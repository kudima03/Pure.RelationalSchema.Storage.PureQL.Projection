"""Reads the case data of one version of the Unicode Character Database.

Shared by generate_case_data.py and check_case_mapping.py. A file that is not
in the directory yet is downloaded from https://www.unicode.org/Public/<version>/ucd/.
"""

import os
import urllib.request

FILES = ("UnicodeData.txt", "SpecialCasing.txt", "CaseFolding.txt", "DerivedCoreProperties.txt")
SIGMA = 0x03A3
FINAL_SIGMA = 0x03C2
SMALL_SIGMA = 0x03C3


def fetch(version, directory):
    """The paths of the four files of `version`, downloaded into `directory` if absent."""
    os.makedirs(directory, exist_ok=True)
    paths = {}
    for name in FILES:
        path = os.path.join(directory, name)
        if not os.path.exists(path):
            url = f"https://www.unicode.org/Public/{version}/ucd/{name}"
            request = urllib.request.Request(url, headers={"User-Agent": "ucd-fetch"})
            with urllib.request.urlopen(request) as response, open(path, "wb") as out:
                out.write(response.read())
        paths[name] = path
    return paths


def rows(path):
    """The fields of every data line of a UCD file, comments and blank lines dropped."""
    with open(path, encoding="utf-8") as lines:
        for line in lines:
            line = line.split("#", 1)[0].strip()
            if line:
                yield [field.strip() for field in line.split(";")]


def sequence(field):
    return tuple(int(code, 16) for code in field.split())


def load(version, directory):
    """The full mappings of `version`: {"upper" | "lower" | "fold": {code point: tuple}},
    without identities, and the code points of Cased and Case_Ignorable as sets."""
    paths = fetch(version, directory)
    with open(paths["CaseFolding.txt"], encoding="utf-8") as lines:
        header = lines.readline()
    if f"CaseFolding-{version}.txt" not in header:
        raise SystemExit(f"{paths['CaseFolding.txt']} is not of Unicode {version}: {header.strip()}")

    upper, lower, fold = {}, {}, {}
    for fields in rows(paths["UnicodeData.txt"]):
        code = int(fields[0], 16)
        if fields[12]:
            upper[code] = sequence(fields[12])
        if fields[13]:
            lower[code] = sequence(fields[13])

    # code; lower; title; upper; (conditions;)? Only the mappings without a condition:
    # Final_Sigma is implemented in code, and the others depend on a language.
    for fields in rows(paths["SpecialCasing.txt"]):
        code = int(fields[0], 16)
        conditions = fields[4] if len(fields) > 4 else ""
        if conditions:
            continue
        lower[code] = sequence(fields[1])
        upper[code] = sequence(fields[3])

    # code; status; mapping. C and F together are the full folding.
    for fields in rows(paths["CaseFolding.txt"]):
        if fields[1] in ("C", "F"):
            code = int(fields[0], 16)
            if code in fold:
                raise SystemExit(f"two full foldings of {code:04X}")
            fold[code] = sequence(fields[2])

    properties = {"Cased": set(), "Case_Ignorable": set()}
    for fields in rows(paths["DerivedCoreProperties.txt"]):
        if fields[1] in properties:
            first, _, last = fields[0].partition("..")
            properties[fields[1]].update(range(int(first, 16), int(last or first, 16) + 1))

    mappings = {"upper": upper, "lower": lower, "fold": fold}
    for mapping in mappings.values():
        for code in [code for code, target in mapping.items() if target == (code,)]:
            del mapping[code]
    return mappings, properties["Cased"], properties["Case_Ignorable"]
