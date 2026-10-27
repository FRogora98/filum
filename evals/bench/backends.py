"""The models a run asks (spec 043): any OpenAI-compatible endpoint with the user's own key, or Claude Code (`claude -p`),
as the published runs were made. A Filum arm reaches filum-mcp over stdio either way."""

import atexit
import json
import os
import re
import shutil
import subprocess
import tempfile
import threading
import time
from dataclasses import dataclass, field
from pathlib import Path

from common import PERSONA
from mcp_client import Mcp
from money import WORST_OUTPUT_TOKENS, cost

LIMIT = re.compile(r"rate.?limit|429|usage limit|session limit|quota|overloaded|too many requests", re.I)
# A tool's text given back to the model is cut here, as a host would.
MAX_TOOL_RESULT = 20_000
# OpenAI takes back at most this many tool calls in one assistant message; a turn with more is given back in parts.
MAX_TOOL_CALLS_PER_MESSAGE = 128


@dataclass
class Answer:
    text: str = ""
    error: str | None = None
    limited: bool = False
    stopped: bool = False  # the budget refused a call
    tools: list = field(default_factory=list)
    results: list = field(default_factory=list)  # (tool, text) of every call
    calls: list = field(default_factory=list)  # (tool, arguments as JSON) of every call, in order
    hooks: list = field(default_factory=list)  # (event, stdout, seconds) of every hook Claude Code ran
    input_tokens: int = 0
    cached_tokens: int = 0
    output_tokens: int = 0
    cost_usd: float = 0.0
    seconds: float = 0.0

    def row(self):
        return {"answer": self.text, "error": self.error, "tools": self.tools, "tool_calls": len(self.tools),
                "input_tokens": self.input_tokens, "cached_tokens": self.cached_tokens, "output_tokens": self.output_tokens,
                "cost_usd": round(self.cost_usd, 6), "seconds": round(self.seconds, 1)}


class OpenAiBackend:
    """Chat completions on OPENAI_BASE_URL (or OpenAI's) with OPENAI_API_KEY, read from the environment only."""

    label = "usd"

    def __init__(self, model, prices, budget, max_turns=12, max_output=WORST_OUTPUT_TOKENS):
        from openai import OpenAI

        if not os.environ.get("OPENAI_API_KEY"):
            raise SystemExit("Set OPENAI_API_KEY (and OPENAI_BASE_URL for an endpoint other than OpenAI's).")
        self.client = OpenAI(base_url=os.environ.get("OPENAI_BASE_URL") or None)
        self.model, self.prices, self.budget, self.max_turns, self.max_output = model, prices, budget, max_turns, max_output

    def ask(self, prompt, filum=None, max_turns=None, max_output=None, context=None):
        """One question: with `filum` = (filum-mcp, home, env), the model may call the memory's tools. Each call asks for
        at most `max_output` tokens, the worst case its booking assumed. `context` is what hooks would add (spec 050)."""
        limit = max_output or self.max_output
        started, answer = time.monotonic(), Answer()
        mcp = Mcp(*filum) if filum else None
        try:
            system = f"{mcp.instructions}\n\n{PERSONA}" if mcp else PERSONA
            if context:
                system = f"{system}\n\n{context}"
            messages = [{"role": "system", "content": system}, {"role": "user", "content": prompt}]
            tools = mcp.functions() if mcp else None
            for _ in range(max_turns or self.max_turns):
                message = self._chat(messages, tools, answer, limit)
                if message is None:
                    break
                if message.content:
                    answer.text = f"{answer.text}\n\n{message.content}".strip()
                if not message.tool_calls:
                    messages.append(message.model_dump(exclude_none=True))
                    break
                calls = message.tool_calls
                for part in range(0, len(calls), MAX_TOOL_CALLS_PER_MESSAGE):
                    group = calls[part:part + MAX_TOOL_CALLS_PER_MESSAGE]
                    messages.append({"role": "assistant", "content": message.content if part == 0 else None,
                                     "tool_calls": [{"id": c.id, "type": "function", "function": {"name": c.function.name, "arguments": c.function.arguments or "{}"}} for c in group]})
                    for call in group:
                        messages.append({"role": "tool", "tool_call_id": call.id, "content": self._call(mcp, call, answer)[:MAX_TOOL_RESULT]})
        except Exception as e:
            answer.error = f"{type(e).__name__}: {str(e)[:300]}"
            answer.limited = bool(LIMIT.search(answer.error))
        finally:
            if mcp:
                mcp.close()
        answer.seconds = time.monotonic() - started
        return answer

    @staticmethod
    def _call(mcp, call, answer):
        name = call.function.name
        answer.tools.append(name)
        answer.calls.append((name, call.function.arguments or "{}"))
        try:
            result = mcp.tool(name, json.loads(call.function.arguments or "{}"))
        except Exception as e:  # a failed tool is the model's to read, as in a host
            result = f"Error: {e}"
        answer.results.append((name, result))
        return result

    def _chat(self, messages, tools, answer, limit):
        from openai import RateLimitError

        reserved = self.budget.reserve(self.prices, sum(len(json.dumps(m)) for m in messages) + len(json.dumps(tools or [])), limit)
        if reserved is None:
            answer.stopped = True
            return None
        response = None
        try:
            for attempt in range(6):
                try:
                    response = self.client.chat.completions.create(model=self.model, messages=messages, tools=tools or None, temperature=0, max_tokens=limit)
                    break
                except RateLimitError:
                    time.sleep(min(60, 2 ** attempt * 2))
            if response is None:
                raise RuntimeError("rate limited six times")
        finally:
            usage = getattr(response, "usage", None)
            cached = getattr(getattr(usage, "prompt_tokens_details", None), "cached_tokens", 0) or 0
            dollars = cost(self.prices, usage.prompt_tokens, cached, usage.completion_tokens) if usage else 0.0
            self.budget.settle(reserved, dollars)
            if usage:
                answer.input_tokens += usage.prompt_tokens
                answer.cached_tokens += cached
                answer.output_tokens += usage.completion_tokens
                answer.cost_usd += dollars
        return response.choices[0].message


# What a run's model must not see of the person running it: the account's identity, Claude Code's own memory and
# CLAUDE.md files (spec 050). Without this, a session without hooks answered "your email is ..." from the account.
IDENTITY = ("emailAddress", "displayName", "fullName", "organizationName")
# A long-lived login for scripts (`claude setup-token`); when set, runs use it and never touch the user's own login.
TOKEN = "CLAUDE_CODE_OAUTH_TOKEN"
ISOLATED_ENV = {"CLAUDE_CODE_DISABLE_AUTO_MEMORY": "1", "CLAUDE_CODE_DISABLE_CLAUDE_MDS": "1"}
# A login that ends sooner than this is renewed by the user's own Claude Code before it is copied.
LOGIN_MARGIN_SECONDS = 30 * 60


class IsolatedConfig:
    """A Claude Code config folder for a whole run: the subscription's login and nothing of the person. Claude Code
    fetches the account's profile into a new folder at its first start, so the folder is started once with a call that
    asks nothing, and the identity is blanked before every run."""

    def __init__(self, claude, model, home=None):
        self.claude, self.model, self.home = claude, model, Path(home or Path.home())
        self.directory = Path(tempfile.mkdtemp(prefix="filum-bench-config-"))
        self.lock, self.started, self.login_at = threading.Lock(), False, None

    def env(self):
        """The environment that points `claude -p` at the folder, ready for a run. With CLAUDE_CODE_OAUTH_TOKEN (from
        `claude setup-token`) the user's login is never copied: a copy and the user's own Claude Code would renew one
        login in turn, and each renewal ends the other's (seen on 2026-10-20)."""
        with self.lock:
            if not os.environ.get(TOKEN):
                self._renew_login()
            if not self.started:
                self.started = True
                subprocess.run([self.claude, "-p", "--model", self.model, "--tools", "", "--no-session-persistence", "Reply ok."],
                               env=self._env(), capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
            self._blank()
            return self._env()

    def _env(self):
        env = {k: v for k, v in os.environ.items() if k not in ("ANTHROPIC_API_KEY", "CLAUDE_CONFIG_DIR")}
        if env.get(TOKEN):
            (self.directory / ".credentials.json").unlink(missing_ok=True)
        return {**env, **ISOLATED_ENV, "CLAUDE_CONFIG_DIR": str(self.directory)}

    def _renew_login(self):
        """The login comes from the user's own config, renewed there when it ends soon, never in the copy."""
        path = self.home / ".claude" / ".credentials.json"
        if json.loads(path.read_text(encoding="utf-8"))["claudeAiOauth"].get("expiresAt", 0) / 1000 - time.time() < LOGIN_MARGIN_SECONDS:
            subprocess.run([self.claude, "-p", "--model", self.model, "--tools", "", "--no-session-persistence", "Reply ok."],
                           capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
        login = json.loads(path.read_text(encoding="utf-8"))["claudeAiOauth"]
        if login.get("expiresAt") != self.login_at:
            self.login_at = login.get("expiresAt")
            (self.directory / ".credentials.json").write_text(json.dumps({"claudeAiOauth": login}), encoding="utf-8")

    def _blank(self):
        path = self.directory / ".claude.json"
        config = json.loads(path.read_text(encoding="utf-8")) if path.exists() else {}
        config["oauthAccount"] = {**(config.get("oauthAccount") or {}), **{k: "" for k in IDENTITY}}
        path.write_text(json.dumps(config), encoding="utf-8")

    def close(self):
        shutil.rmtree(self.directory, ignore_errors=True)


class ClaudeCodeBackend:
    """`claude -p` on the user's Claude Code: its costs are the equivalent dollars it reports, not money paid. Its runs
    have their own config folder, so the model sees nothing of the person running them (`IsolatedConfig`)."""

    label = "usd-equivalent"

    def __init__(self, model, budget):
        self.claude = shutil.which("claude") or "claude"
        self.model, self.budget = model, budget
        self.config = IsolatedConfig(self.claude, model)
        atexit.register(self.config.close)

    def ask_with_hooks(self, prompt, filum, settings, max_turns=None):
        """As Claude Code runs with Filum's hooks: `settings` go in the session's project settings (spec 050)."""
        return self.ask(prompt, filum=filum, max_turns=max_turns, settings=settings)

    def ask(self, prompt, filum=None, max_turns=None, max_output=None, settings=None):
        if self.budget.stopped:
            return Answer(stopped=True)
        with tempfile.TemporaryDirectory(prefix="filum-bench-") as cwd:
            servers = {}
            if filum:
                exe, home, env = filum
                servers["filum"] = {"command": str(exe), "args": [], "env": {**(env or {}), "FILUM_HOME": str(home)}}
            config = Path(cwd) / "mcp.json"
            config.write_text(json.dumps({"mcpServers": servers}), encoding="utf-8")
            if settings:
                (Path(cwd) / ".claude").mkdir()
                (Path(cwd) / ".claude" / "settings.json").write_text(json.dumps(settings), encoding="utf-8")
            env = self.config.env()
            # The memory's own settings (FILUM_TODAY among them) reach the hooks Claude Code runs too.
            env.update((filum[2] or {}) if filum else {})
            command = [self.claude, "-p", "--model", self.model, "--output-format", "stream-json", "--verbose", "--include-hook-events",
                       "--setting-sources", "project", "--strict-mcp-config", "--mcp-config", str(config),
                       "--tools", "", "--disable-slash-commands", "--no-session-persistence", "--append-system-prompt", PERSONA]
            if filum:
                command += ["--allowedTools", "mcp__filum__*"]
            started = time.monotonic()
            lines, times, stderr, returncode = _stream(command, prompt, cwd, env)
        answer = parse_stream("\n".join(lines), times)
        answer.seconds = time.monotonic() - started
        if answer.error is None and not answer.text and returncode:
            answer.error = f"exit {returncode}: {stderr.strip()[:300]}"
        answer.limited = bool(answer.error and LIMIT.search(answer.error + stderr))
        self.budget.add(answer.cost_usd)
        return answer


def _stream(command, prompt, cwd, env=None):
    """Runs `claude -p`, noting when each line of its output arrived: a hook's time is from its start to its response."""
    with tempfile.TemporaryFile(mode="w+", encoding="utf-8", errors="replace") as err:
        process = subprocess.Popen(command, cwd=cwd, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=err,
                                   text=True, encoding="utf-8", errors="replace")
        process.stdin.write(prompt)
        process.stdin.close()
        lines, times = [], []
        for line in process.stdout:
            lines.append(line.rstrip("\n"))
            times.append(time.monotonic())
        returncode = process.wait()
        err.seek(0)
        return lines, times, err.read(), returncode


def parse_stream(stdout, times=None):
    """A `claude -p` stream-json transcript: every text the agent wrote, its tool calls and results, the hooks it ran,
    its usage. `times` is when each line arrived, for the hooks' time."""
    answer, texts, result, names, started = Answer(), [], None, {}, {}
    for number, line in enumerate(stdout.splitlines()):
        try:
            item = json.loads(line)
        except json.JSONDecodeError:
            continue
        at = times[number] if times and number < len(times) else None
        if item.get("subtype") == "hook_started":
            started[item.get("hook_id")] = at
        elif item.get("subtype") == "hook_response":
            began = started.get(item.get("hook_id"))
            answer.hooks.append((item.get("hook_event"), item.get("stdout") or "", at - began if at is not None and began is not None else None))
        content = (item.get("message") or {}).get("content")
        for c in content if isinstance(content, list) else []:
            if c.get("type") == "tool_use":
                name = c.get("name", "").removeprefix("mcp__filum__")
                names[c.get("id")] = name
                answer.tools.append(name)
                answer.calls.append((name, json.dumps(c.get("input") or {}, ensure_ascii=False)))
            elif c.get("type") == "text" and c.get("text") and item.get("type") == "assistant":
                texts.append(c["text"])
            elif c.get("type") == "tool_result" and c.get("tool_use_id") in names:
                answer.results.append((names[c["tool_use_id"]], json.dumps(c.get("content"), ensure_ascii=False)))
        if item.get("type") == "result":
            result = item
    usage = (result or {}).get("usage") or {}
    answer.text = "\n\n".join(texts) if texts else (result or {}).get("result") or ""
    answer.input_tokens = usage.get("input_tokens", 0) + usage.get("cache_creation_input_tokens", 0) + usage.get("cache_read_input_tokens", 0)
    answer.cached_tokens = usage.get("cache_read_input_tokens", 0)
    answer.output_tokens = usage.get("output_tokens", 0)
    answer.cost_usd = (result or {}).get("total_cost_usd", 0) or 0
    if result is None:
        answer.error = "no result"
    elif result.get("is_error"):
        answer.error = f"{result.get('subtype')}: {str(result.get('result'))[:300]}"
    return answer
