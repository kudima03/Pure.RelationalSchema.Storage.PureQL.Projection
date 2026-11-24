#!/usr/bin/env python3
"""Generates RegexVectors.cs: patterns of the regular expression dialect of PureQL
with operands, and what Python's `re` gives for each, for the tests of matchesRegex,
extractRegex and replaceRegex.

    python3 -I generate_regex_vectors.py <RegexVectors.cs>

Python's `re` matches by code point with the backtracking rule, as the dialect does.
A pattern is written twice from the same random tokens: in the dialect, and for
Python, where the only difference is `$`, written `\\Z` (Python's `$` also matches
before a final line feed). Python compiles it with re.ASCII, which makes \\d \\w \\s
\\b the ASCII sets of the dialect. The run is seeded, so it writes the same file
every time; a row is

    pattern, operand, replacement, replaceRegex, extractRegex, group 1, group 2, ...

with null for no match and for a group that took no part in it.

A second table holds text that is not a pattern: random runs of the characters
patterns are made of that the token check of the schema of the specification
(REGEX, copied from tools/generate_schema.py) rejects, or that it accepts and
Python cannot compile (parentheses that do not balance, a range or a {n,m} out of
order).

Python holds a surrogate without its pair as a code point of its own, as the
interpreter does, but a high one directly before a low one is a pair in UTF-16, so
a row uses only high ones or only low ones.
"""

import random
import re
import sys

SEED = 20260214
PATTERNS = 900
WIDTH = 90

LITERALS = list("abcx01_- :@/AZ") + ["\u00E9", "\u00DF", "\u0436", "\U0001F600", "\U0001F60F", "\U0001D4B3"]
SPECIAL = list("\\()[]{}*+?|^$./")
SETS = ["\\d", "\\D", "\\w", "\\W", "\\s", "\\S"]
CONTROLS = ["\\t", "\\n", "\\r"]
OPERAND = LITERALS + list("bB9zq.\n\n\t\r\v\f$^*(") + [
    "\u00A0", "\u2028", "\u0663", "\u0130", "\u0301", "\uFFFF", "\U00010000", "\U0010FFFF", "\U0001F601",
]
REPLACEMENTS = ["-", "", "$1", "\\1", "$&", "<$0>", "\U0001F600", "${1}x", "$$"]
RANGES = [
    ("a", "c"), ("0", "9"), ("A", "Z"), ("a", "z"), (" ", "/"), ("a", "\u00E9"), ("\u00DF", "\u0436"),
    ("\U0001F600", "\U0001F60F"), ("z", "\U0001F600"), ("\U0001D4B3", "\U0001F600"), ("\uFFFF", "\U00010000"),
    ("\U00010000", "\U0010FFFF"), ("\u0000", "\uFFFF"), ("-", "a"), ("+", "-"), ("\u0100", "\U00010400"),
]
# The token check of a literal pattern, as tools/generate_schema.py of the
# specification 0.1.0-preview.3.0.0 has it.
REGEX_ATOM = (
    r"[^\\()\[\]{}*+?|^$.]"
    r"|\\([dDwWsSnrt]|[\\()\[\]{}*+?|^$./])"
    r"|\[\^?([^\]\[\\^]|\\([dDwWsSnrt]|[\\()\[\]{}*+?|^$./-]))+\]"
    r"|\.|\)"
)
REGEX_QUANTIFIER = r"([*+?]|\{[0-9]+(,[0-9]*)?\})\??"
REGEX = re.compile(rf"(({REGEX_ATOM})({REGEX_QUANTIFIER})?|\((\?:)?|[|^$]|\\[bB])*")
NOT_PATTERNS = 600
PIECES = list("ab1-,^$.|*+?()[]{}\\") + [
    "\\d", "\\b", "\\n", "\\.", "\\-", "\\\\", "\\1", "\\p", "\\x", "(?:", "(?", "[^", "{2}", "{1,3}", "{3,1}",
    "{2,}", "{,2}", "[a-c]", "[c-a]", "a-c", "\\w-", "?:", "?=", "?<", "\U0001F600", "\u00E9", " ",
]
HIGH = "\uD83D"
LOW = "\uDE00"


class Maker:
    """Writes random patterns; `dialect` and `python` grow together."""

    def __init__(self, randomness, surrogate):
        self.random = randomness
        self.surrogate = surrogate
        self.groups = 0

    def literal(self):
        if self.surrogate and self.random.random() < 0.3:
            return self.surrogate
        return self.random.choice(LITERALS)

    def klass(self):
        members = []
        for _ in range(self.random.randint(1, 4)):
            kind = self.random.random()
            if kind < 0.35:
                char = self.literal()
                members.append("\\-" if char == "-" else char)
            elif kind < 0.6:
                first, last = self.random.choice(RANGES)
                first = "\\" + first if first in "\\]^[-" else first
                last = "\\" + last if last in "\\]^[-" else last
                members.append(first + "-" + last)
            elif kind < 0.8:
                members.append(self.random.choice(SETS))
            elif kind < 0.9:
                members.append(self.random.choice(CONTROLS))
            else:
                members.append("\\" + self.random.choice(SPECIAL + ["-"]))
        if self.random.random() < 0.15:
            members.insert(0, "-")
        if self.random.random() < 0.15:
            members.append("-")
        negation = "^" if self.random.random() < 0.3 else ""
        return "[" + negation + "".join(members) + "]"

    def atom(self, depth):
        kind = self.random.random()
        if kind < 0.40:
            char = self.literal()
            return char, char
        if kind < 0.47:
            text = "\\" + self.random.choice(SPECIAL)
            return text, text
        if kind < 0.57:
            text = self.random.choice(SETS)
            return text, text
        if kind < 0.60:
            text = self.random.choice(CONTROLS)
            return text, text
        if kind < 0.70:
            return ".", "."
        if kind < 0.82 or depth >= 3:
            text = self.klass()
            return text, text
        if self.random.random() < 0.6:
            self.groups += 1
            start = "("
        else:
            start = "(?:"
        dialect, python = self.alternatives(depth + 1)
        return start + dialect + ")", start + python + ")"

    def quantifier(self):
        kind = self.random.random()
        if kind < 0.25:
            text = "*"
        elif kind < 0.5:
            text = "+"
        elif kind < 0.7:
            text = "?"
        else:
            least = self.random.randint(0, 2)
            form = self.random.random()
            if form < 0.4:
                text = "{%d}" % least
            elif form < 0.6:
                text = "{%d,}" % least
            else:
                text = "{%d,%d}" % (least, least + self.random.randint(0, 2))
        return text + ("?" if self.random.random() < 0.3 else "")

    def sequence(self, depth):
        dialect, python = [], []
        for _ in range(self.random.choice([0, 1, 1, 2, 2, 3, 3, 4])):
            kind = self.random.random()
            if kind < 0.05:
                dialect.append("^")
                python.append("^")
            elif kind < 0.11:
                dialect.append("$")
                python.append("\\Z")
            elif kind < 0.17:
                text = self.random.choice(["\\b", "\\B"])
                dialect.append(text)
                python.append(text)
            else:
                ours, theirs = self.atom(depth)
                if self.random.random() < 0.4:
                    quantifier = self.quantifier()
                    ours += quantifier
                    theirs += quantifier
                dialect.append(ours)
                python.append(theirs)
        return "".join(dialect), "".join(python)

    def alternatives(self, depth):
        count = self.random.choice([1, 1, 1, 2, 2, 3])
        parts = [self.sequence(depth) for _ in range(count)]
        return "|".join(part[0] for part in parts), "|".join(part[1] for part in parts)


def operands(randomness, pattern, surrogate):
    """Three operands: one of anything, two mostly of what the pattern is made of."""
    own = [char for char in pattern if char not in "\\()[]{}*+?|^$"] or ["a"]
    result = []
    for source in (OPERAND, own, own + OPERAND[:6]):
        chars = [randomness.choice(source) for _ in range(randomness.randint(0, 8))]
        if surrogate:
            chars.insert(randomness.randint(0, len(chars)), surrogate)
        if randomness.random() < 0.15:
            chars.append("\n")
        result.append("".join(chars))
    return result


def replace(compiled, operand, replacement):
    """replaceRegex as the specification defines it: after an empty match the search
    moves on by one code point. That is the rule of .NET, Java and JavaScript; re.sub
    of Python (and Perl) instead looks for a match that is not empty at the same
    position, so `x*` and ` ??` differ on a space: this loop is used, not re.sub."""
    result = []
    copied = position = 0
    while True:
        match = compiled.search(operand, position)
        if not match:
            break
        result.append(operand[copied : match.start()])
        result.append(replacement)
        copied = position = match.end()
        if match.start() == match.end():
            if match.end() == len(operand):
                break
            position += 1
    result.append(operand[copied:])
    return "".join(result)


def candidates(randomness, count):
    """Random runs of the pieces patterns are made of; most are not patterns."""
    seen = set()
    while len(seen) < count:
        text = "".join(randomness.choice(PIECES) for _ in range(randomness.randint(1, 7)))
        if text not in seen:
            seen.add(text)
            yield text


def compiles(text):
    try:
        re.compile(text, re.ASCII)
    except (re.error, OverflowError):
        return False
    return True


def not_patterns(randomness):
    result = []
    for text in candidates(randomness, 100000):
        if not REGEX.fullmatch(text) or not compiles(text):
            result.append(text)
            if len(result) == NOT_PATTERNS:
                return result


def units(text):
    data = text.encode("utf-16-be", "surrogatepass")
    return [int.from_bytes(data[index : index + 2], "big") for index in range(0, len(data), 2)]


def escaped(unit):
    char = chr(unit)
    if char in '"\\':
        return "\\" + char
    return char if 0x20 <= unit < 0x7F else f"\\u{unit:04X}"


def literal(text, indent):
    """A C# expression for the string, in ASCII, on lines that fit."""
    if text is None:
        return "null"
    pieces = [escaped(unit) for unit in units(text)]
    room = WIDTH - len(indent) - 5
    lines, line = [], ""
    for piece in pieces:
        if len(line) + len(piece) > room:
            lines.append(line)
            line = ""
        line += piece
    lines.append(line)
    return f'"\n{indent}+ "'.join(lines).join('""')


def row(values):
    one = "        [" + ", ".join(literal(value, "") for value in values) + "],"
    if len(one) <= WIDTH and "\n" not in one:
        return [one]
    indent = " " * 12
    lines = ["        ["]
    for value in values:
        lines.append(indent + literal(value, indent + "    ") + ",")
    lines.append("        ],")
    return lines


def main():
    if len(sys.argv) != 2:
        raise SystemExit(__doc__)
    randomness = random.Random(SEED)
    rows = []
    matched = 0
    seen = set()
    while len(seen) < PATTERNS:
        surrogate = randomness.choice([None] * 8 + [HIGH, LOW])
        maker = Maker(randomness, surrogate)
        dialect, python = maker.alternatives(0)
        if dialect in seen:
            continue
        seen.add(dialect)
        compiled = re.compile(python, re.ASCII)
        assert compiled.groups == maker.groups
        for operand in operands(randomness, dialect, surrogate):
            if not operand and "\\B" in dialect:
                # Before Python 3.14, \B never matches in an empty string, where no
                # position is a boundary; the dialect has no such exception.
                operand = "a"
            replacement = randomness.choice(REPLACEMENTS)
            match = compiled.search(operand)
            matched += match is not None
            groups = [match.group(number) if match else None for number in range(1, maker.groups + 1)]
            replaced = replace(compiled, operand, replacement)
            rows.append([dialect, operand, replacement, replaced, match.group(0) if match else None, *groups])

    lines = [
        "namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;",
        "",
        "// Patterns of the regular expression dialect with operands, and what Python's re",
        "// (which matches by code point, with the backtracking rule) gives for each.",
        "// Generated by tools/generate_regex_vectors.py; never edit it by hand.",
        "internal static class RegexVectors",
        "{",
        "    // A pattern, an operand, a replacement, then replaceRegex, extractRegex and",
        "    // extractRegex of each group of the pattern; null where nothing matches and",
        "    // where a group took no part in the match.",
        "    public static readonly string?[][] Rows =",
        "    [",
    ]
    for values in rows:
        lines += row(values)
    lines += [
        "    ];",
        "",
        "    // Text that is not a pattern of the dialect: the token check of the schema",
        "    // rejects it, or Python cannot compile it.",
        "    public static readonly string[] NotPatterns =",
        "    [",
    ]
    rejected = not_patterns(random.Random(SEED + 1))
    for value in rejected:
        lines.append("        " + literal(value, " " * 12) + ",")
    lines += ["    ];", "}"]
    text = "\n".join(lines) + "\n"
    long = [line for line in text.split("\n") if len(line) > WIDTH]
    assert not long, long[0]
    with open(sys.argv[1], "w", encoding="utf-8", newline="\n") as out:
        out.write(text)
    print(
        f"{sys.argv[1]}: {len(rows)} rows of {len(seen)} patterns, {matched} with a match,"
        f" and {len(rejected)} texts that are not patterns"
    )


if __name__ == "__main__":
    main()
