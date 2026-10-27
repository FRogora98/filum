"""What a run costs (spec 043): every call priced from its usage, a cap given before the run, never passed."""

import threading

# USD per million tokens: (input, cached input, output), from the providers' price pages. A model not here needs
# --price-in and --price-out; a run on a model with no price does not start.
PRICES = {
    "gpt-4o-mini": (0.15, 0.075, 0.60),
    "gpt-4o-mini-2024-07-18": (0.15, 0.075, 0.60),
    "gpt-4o": (2.50, 1.25, 10.00),
    "gpt-4o-2024-08-06": (2.50, 1.25, 10.00),
    "gpt-4.1-mini": (0.40, 0.10, 1.60),
    "gpt-4.1": (2.00, 0.50, 8.00),
}

# A call's worst case, booked before it is sent: its prompt at full price (about 3 characters a token) and its longest
# answer. The request asks for no more than that (max_tokens), so the booking holds.
WORST_OUTPUT_TOKENS = 2_000
CHARS_PER_TOKEN = 3


def price(model, price_in=None, price_out=None):
    """(input, cached, output) USD per million tokens; None when unknown."""
    if price_in is not None and price_out is not None:
        return (price_in, price_in, price_out)
    return PRICES.get(model)


class Budget:
    """The dollars a run spends, against its cap. Calls running at once book their worst case first, so together they
    cannot pass the cap; each settles at its real cost."""

    def __init__(self, cap, spent=0.0):
        self.cap, self.spent, self.stopped = cap, spent, False
        self.lock = threading.Lock()

    def reserve(self, prices, prompt_chars, max_output=WORST_OUTPUT_TOKENS):
        """The booked worst case of a call, or None (and the run stopped) when it could pass the cap."""
        worst = (prompt_chars / CHARS_PER_TOKEN * prices[0] + max_output * prices[2]) / 1e6
        with self.lock:
            if self.stopped or self.spent + worst > self.cap:
                self.stopped = True
                return None
            self.spent += worst
            return worst

    def settle(self, reserved, dollars):
        with self.lock:
            self.spent += dollars - reserved

    def add(self, dollars):
        """A cost known only after the call (Claude Code's equivalent dollars): the run stops once it reaches the cap."""
        with self.lock:
            self.spent += dollars
            if self.spent >= self.cap:
                self.stopped = True


def cost(prices, prompt_tokens, cached_tokens, output_tokens):
    return ((prompt_tokens - cached_tokens) * prices[0] + cached_tokens * prices[1] + output_tokens * prices[2]) / 1e6


def estimate(prices, calls):
    """The dollars of a list of (input tokens, output tokens), at full input price."""
    return sum(i * prices[0] + o * prices[2] for i, o in calls) / 1e6
