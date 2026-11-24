#!/usr/bin/env python3
"""Cross-checks upper, lower and foldCase against Python, and writes the vectors of
the tests.

    python3 -I check_case_mapping.py 16.0.0 <directory of the UCD files> [CaseMappingVectors.cs]

It holds a reference implementation of toUppercase, toLowercase (with Final_Sigma)
and toCasefold that reads the Unicode Character Database directly, and

1. runs the reference of Python's own Unicode version (unicodedata.unidata_version,
   downloaded next to the given directory) against str.upper, str.lower and
   str.casefold for every code point and for a corpus of strings around a sigma:
   the algorithm and the reading of the files are then the ones Python has;
2. compares the reference of the requested version with Python for every code point
   that Python's version assigns, and prints each difference (a mapping the later
   version added or changed);
3. prints the SHA-256 of the three mappings of every code point in the requested
   version, the constant the tests compare PureQLText with;
4. writes the corpus of strings, with what the reference gives, as a C# file.
"""

import hashlib
import itertools
import os
import random
import sys
import unicodedata

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ucd  # noqa: E402


class Reference:
    def __init__(self, version, directory):
        self.version = version
        self.mappings, self.cased, self.ignorable = ucd.load(version, directory)

    def _map(self, kind, code):
        return "".join(map(chr, self.mappings[kind].get(code, (code,))))

    def upper(self, text):
        return "".join(self._map("upper", ord(c)) for c in text)

    def fold(self, text):
        return "".join(self._map("fold", ord(c)) for c in text)

    def lower(self, text):
        result = []
        for index, char in enumerate(text):
            if ord(char) == ucd.SIGMA and self._final(text, index):
                result.append(chr(ucd.FINAL_SIGMA))
            else:
                result.append(self._map("lower", ord(char)))
        return "".join(result)

    def _final(self, text, index):
        """Final_Sigma: a cased letter before, across case-ignorable code points, and
        none after. A code point that is both is skipped as ignorable, as ICU and
        Python do."""
        before = index - 1
        while before >= 0 and ord(text[before]) in self.ignorable:
            before -= 1
        if before < 0 or ord(text[before]) not in self.cased:
            return False
        after = index + 1
        while after < len(text) and ord(text[after]) in self.ignorable:
            after += 1
        return after == len(text) or ord(text[after]) not in self.cased


def digest(reference):
    """SHA-256 over one line per code point: its number and its upper case, lower
    case and folding, each as the hexadecimal UTF-16 code units of the result."""
    sha = hashlib.sha256()
    for code in range(0x110000):
        char = chr(code)
        fields = [f"{code:X}"]
        for text in (reference.upper(char), reference.lower(char), reference.fold(char)):
            units = text.encode("utf-16-be", "surrogatepass")
            fields.append(units.hex().upper())
        sha.update((";".join(fields) + "\n").encode("ascii"))
    return sha.hexdigest()


# Around a sigma: cased letters, case-ignorable code points, code points that are both
# (a modifier letter, the combining iota subscript), and others; all assigned, with the
# same properties, since long before either version.
ALPHABET = [
    "\u03A3", "\u03C3", "\u03C2", "A", "b", "\u0391", "\u0434", "\U00010400",
    "'", ".", ":", "\u00AD", "\u0301", "\u2019", "\U000E0041",
    "\u02B0", "\u0345", "\u1D2C",
    " ", "1", "-", "\u4E2D", "\U0001F600",
]


def corpus():
    strings = ["", "\u03A3", "\u039F\u0394\u039F\u03A3", "\u03A3\u03A3", "\u0391\u03A3\u0391"]
    others = [c for c in ALPHABET if c != "\u03A3"]
    for before, after in itertools.product([""] + others, repeat=2):
        strings.append(before + "\u03A3" + after)
    randomness = random.Random(20260101)
    for _ in range(400):
        length = randomness.randint(2, 7)
        strings.append("".join(randomness.choice(ALPHABET) for _ in range(length)))
    for before, middle, after in itertools.product(["A", "1", ""], ["'", "\u0301", "\u02B0"], ["A", "1", ""]):
        strings.append(before + middle * 3 + "\u03A3" + middle * 3 + after)
    return list(dict.fromkeys(strings))


def literal(text):
    """A C# string literal in ASCII."""
    result = []
    units = text.encode("utf-16-be", "surrogatepass")
    for index in range(0, len(units), 2):
        unit = int.from_bytes(units[index : index + 2], "big")
        char = chr(unit)
        if 0x20 <= unit < 0x7F and char not in '"\\':
            result.append(char)
        else:
            result.append(f"\\u{unit:04X}")
    return '"' + "".join(result) + '"'


def write_vectors(path, reference, strings):
    lines = [
        "namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;",
        "",
        "// Strings around a sigma with what lower gives for each, generated by",
        f"// tools/check_case_mapping.py from the Unicode Character Database {reference.version}",
        "// and equal to str.lower of Python. Never edit it by hand: run the script.",
        "internal static class CaseMappingVectors",
        "{",
        "    // An operand, then its lower case.",
        "    public static readonly string[][] Lower =",
        "    [",
    ]
    for text in strings:
        row = f"        [{literal(text)}, {literal(reference.lower(text))}],"
        if len(row) > 90:
            row = f"        [\n            {literal(text)},\n            {literal(reference.lower(text))},\n        ],"
        lines.append(row)
    lines += ["    ];", "}"]
    text = "\n".join(lines) + "\n"
    assert all(len(line) <= 90 for line in text.split("\n")), "a line is too long"
    with open(path, "w", encoding="utf-8", newline="\n") as out:
        out.write(text)
    print(f"{path}: {len(strings)} strings")


def main():
    if len(sys.argv) not in (3, 4):
        raise SystemExit(__doc__)
    version, directory = sys.argv[1:3]
    python_version = unicodedata.unidata_version
    reference = Reference(version, directory)
    strings = corpus()

    # 1. The reference of Python's version is Python.
    own = Reference(python_version, os.path.join(os.path.dirname(os.path.abspath(directory)), python_version))
    for code in range(0x110000):
        char = chr(code)
        assert own.upper(char) == char.upper(), f"upper {code:04X}"
        assert own.lower(char) == char.lower(), f"lower {code:04X}"
        assert own.fold(char) == char.casefold(), f"fold {code:04X}"
    for text in strings:
        assert own.upper(text) == text.upper(), f"upper {text!r}"
        assert own.lower(text) == text.lower(), f"lower {text!r}"
        assert own.fold(text) == text.casefold(), f"fold {text!r}"
    print(f"reference of Unicode {python_version} agrees with Python on every code point and on {len(strings)} strings")

    # 2. The requested version against Python, where Python's version assigns the code point.
    differences = 0
    shared = 0
    for code in range(0x110000):
        char = chr(code)
        if unicodedata.category(char) == "Cn":
            continue
        shared += 1
        for name, mine, theirs in (
            ("upper", reference.upper(char), char.upper()),
            ("lower", reference.lower(char), char.lower()),
            ("foldCase", reference.fold(char), char.casefold()),
        ):
            if mine != theirs:
                differences += 1
                was = " ".join(f"{ord(c):04X}" for c in theirs)
                now = " ".join(f"{ord(c):04X}" for c in mine)
                print(f"  {name}(U+{code:04X}): {was} in {python_version}, {now} in {version}")
    print(f"Unicode {version} against Python ({python_version}): {differences} differences in {shared} shared code points")
    for text in strings:
        assert reference.upper(text) == text.upper(), f"upper {text!r}"
        assert reference.lower(text) == text.lower(), f"lower {text!r}"
        assert reference.fold(text) == text.casefold(), f"fold {text!r}"
    print(f"Unicode {version} agrees with Python on the {len(strings)} strings")

    # 3. The constant of the tests.
    print(f"SHA-256 of the mappings of Unicode {version}: {digest(reference)}")

    # 4. The vectors of the tests.
    if len(sys.argv) == 4:
        write_vectors(sys.argv[3], reference, strings)


if __name__ == "__main__":
    main()
