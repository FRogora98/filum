"""filum-mcp over stdio (spec 043): a minimal MCP client, newline-delimited JSON-RPC, enough to list and call tools."""

import json
import os
import subprocess

PROTOCOL_VERSION = "2025-06-18"


class Mcp:
    def __init__(self, exe, home, env=None):
        self.proc = subprocess.Popen([str(exe)], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                     env={**os.environ, **(env or {}), "FILUM_HOME": str(home)}, text=True, encoding="utf-8")
        self.next_id = 0
        init = self.call("initialize", {"protocolVersion": PROTOCOL_VERSION, "capabilities": {}, "clientInfo": {"name": "filum-bench", "version": "1"}})
        self.instructions = init.get("instructions", "")
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized"})
        self.tools = self.call("tools/list", {})["tools"]

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()

    def send(self, message):
        self.proc.stdin.write(json.dumps(message) + "\n")
        self.proc.stdin.flush()

    def call(self, method, params):
        self.next_id += 1
        self.send({"jsonrpc": "2.0", "id": self.next_id, "method": method, "params": params})
        while True:
            line = self.proc.stdout.readline()
            if not line:
                raise RuntimeError("filum-mcp closed")
            message = json.loads(line)
            if message.get("id") == self.next_id:
                if "error" in message:
                    raise RuntimeError(message["error"])
                return message["result"]

    def tool(self, name, arguments):
        """The tool's text; a refused call comes back as text too (isError), for the model to read, as in a host."""
        result = self.call("tools/call", {"name": name, "arguments": arguments})
        return "\n".join(c.get("text", "") for c in result.get("content", []))

    def functions(self):
        """The tools as OpenAI-style function definitions."""
        return [{"type": "function", "function": {"name": t["name"], "description": t.get("description", ""),
                                                  "parameters": t.get("inputSchema", {"type": "object"})}} for t in self.tools]

    def close(self):
        if self.proc.poll() is None:
            self.proc.kill()
            self.proc.wait()
