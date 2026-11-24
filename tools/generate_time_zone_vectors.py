#!/usr/bin/env python3
"""Writes TimeZoneVectors.txt: what the zone functions of PureQLTime must give
around the changes of the clocks of every time zone, computed with Python
`zoneinfo` from the system's IANA time zone database.

    python3 generate_time_zone_vectors.py <time_zones.txt> <TimeZoneVectors.txt>
        [--all] [--from YEAR] [--to YEAR] [--ordinary N] [--check N]

Without --all the file is the sample the tests embed: for every zone a few
changes of its clocks from 1970 on (the largest, the last before the final
year, some picked by a hash, and the unusual ones of the whole database) and a
few ordinary instants. With --all it holds every change in the years asked
for, for a run of the same tests by hand; --ordinary N asks for N ordinary
instants in each zone instead of two.

The reference is independent of the interpreter. The changes of a zone are
found by reading its offset day by day and bisecting to the second, and the
instant of a local date and time is, as the specification words it, the first
instant at which the local date and time are the given ones or later: the
minimum over every stretch of one offset. --check N also confirms N of those
answers second by second.

A line is one of:
    Z <zone>
    T <instant> <local> <start of day> <start of hour>
    F <local> <instant>
Every number is seconds since 1970-01-01T00:00:00, of UTC for an instant and of
the zone's own clock for a local date and time.

Left out, because System.TimeZoneInfo does not hold them: changes to or from an
offset that is not a whole number of minutes (local mean time, until 1972 in
Africa/Monrovia). Before 1970 the database also differs between systems (the
`backzone` zones), so the embedded sample starts in 1970.
"""
import argparse
import bisect
import datetime
import hashlib
import zoneinfo

EPOCH = datetime.datetime(1970, 1, 1, tzinfo=datetime.timezone.utc)
DAY = 86400


def seconds(year):
    moment = datetime.datetime(year, 1, 1, tzinfo=datetime.timezone.utc)
    return int((moment - EPOCH).total_seconds())


def offset(zone, instant):
    moment = EPOCH + datetime.timedelta(seconds=instant)
    return int(moment.astimezone(zone).utcoffset().total_seconds())


def changes(zone, start, end):
    """Every (instant, offset before, offset after) between start and end."""
    found = []
    before, read = offset(zone, start), start
    while read < end:
        ahead = read + DAY
        after = offset(zone, ahead)
        if after != before:
            low, high = read, ahead
            while high - low > 1:
                middle = (low + high) // 2
                if offset(zone, middle) == before:
                    low = middle
                else:
                    high = middle
            found.append((high, before, after))
            before = after
        read = ahead
    return found


class Clock:
    """The stretches of one offset of a zone, and the rule over them."""

    def __init__(self, name, start, end):
        self.name = name
        self.zone = zoneinfo.ZoneInfo(name)
        self.changes = changes(self.zone, start, end)
        self.starts = [change[0] for change in self.changes]
        self.first = offset(self.zone, start)

    def stretches(self, low, high):
        """(start, end, offset) of every stretch that meets low..high."""
        at = bisect.bisect_right(self.starts, low)
        start = self.starts[at - 1] if at else None
        current = self.changes[at - 1][2] if at else self.first
        while True:
            end = self.starts[at] if at < len(self.starts) else None
            yield start, end, current
            if end is None or end > high:
                return
            start, current, at = end, self.changes[at][2], at + 1

    def local(self, instant):
        return instant + offset(self.zone, instant)

    def instant(self, local):
        """The first instant at which the local time is `local` or later."""
        best = None
        for start, end, current in self.stretches(local - 2 * DAY, local + 2 * DAY):
            reached = local - current
            if start is not None and start > reached:
                reached = start
            if (end is None or reached < end) and (best is None or reached < best):
                best = reached
        return best

    def instant_by_seconds(self, local):
        """The same, trying every second: slow, and only to check the above."""
        instant = local - DAY
        while self.local(instant) < local:
            instant += 1
        return instant

    def whole_minutes(self, low, high):
        return all(current % 60 == 0 for _, _, current in self.stretches(low, high))


def vectors(clock, change, brief):
    instant, before, after = change
    middle = (before + after) // 2
    instants = [instant - 1, instant, instant + abs(after - before) // 2]
    locals_ = [
        instant + before - 1,
        instant + before,
        instant + middle,
        instant + after - 1,
        instant + after,
    ]
    if not brief:
        instants += [instant - 5400, instant + 1, instant + 5400, instant + DAY - 1]
        locals_ += [
            instant + before + 1,
            instant + after + 1,
            instant + before - 3 * 3600,
            instant + after + 3 * 3600,
            instant + before + DAY,
            instant + after - DAY,
        ]
    return sorted(set(instants)), sorted(set(locals_))


def lines(clock, instants, locals_):
    out = []
    for instant in instants:
        local = clock.local(instant)
        day = clock.instant(local - local % DAY)
        hour = clock.instant(local - local % 3600)
        out.append(f"T {instant} {local} {day} {hour}")
    for local in locals_:
        out.append(f"F {local} {clock.instant(local)}")
    return out


def rank(name, change):
    return hashlib.sha1(f"{name} {change[0]}".encode()).hexdigest()


def unusual(clock, change):
    """Why a change is worth a test whatever the zone, or None."""
    instant, before, after = change
    jump = after - before
    if jump % 3600:
        return "part of an hour"
    if abs(jump) > 3600:
        return "more than an hour"
    if jump < 0 and (instant + after) // DAY < (instant + before - 1) // DAY:
        return "back across midnight"
    if jump > 0 and (instant + before) % DAY == 0:
        return "forward at midnight"
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("zones")
    parser.add_argument("target")
    parser.add_argument("--all", action="store_true")
    parser.add_argument("--from", dest="start", type=int)
    parser.add_argument("--to", dest="end", type=int, default=2024)
    parser.add_argument("--ordinary", type=int, default=2)
    parser.add_argument("--check", type=int, default=0)
    arguments = parser.parse_args()
    first = arguments.start or (1900 if arguments.all else 1970)
    start, end = seconds(first), seconds(arguments.end)

    with open(arguments.zones, encoding="utf-8") as file:
        names = [
            line.strip() for line in file if line.strip() and not line.startswith("#")
        ]
    with open("/usr/share/zoneinfo/tzdata.zi", encoding="utf-8") as file:
        release = file.readline().strip().removeprefix("# version ")

    out = [
        f"# IANA time zone database {release}, Python zoneinfo; changes of the clocks",
        f"# from {first} up to {arguments.end}. Written by",
        "# tools/generate_time_zone_vectors.py: never edit it by hand.",
    ]
    seen, kinds, count, checked = set(), {}, 0, 0
    for name in names:
        # Three days around the years asked for, so every answer is inside.
        clock = Clock(name, start - 4 * DAY, end + 4 * DAY)
        usable = [
            change
            for change in clock.changes
            if start <= change[0] < end
            and clock.whole_minutes(change[0] - 3 * DAY, change[0] + 3 * DAY)
        ]
        chosen = usable
        if not arguments.all:
            chosen = []
            history = tuple(usable)
            if usable and history not in seen:
                seen.add(history)
                chosen = [
                    max(usable, key=lambda change: abs(change[2] - change[1])),
                    usable[-1],
                    min(usable, key=lambda change: rank(name, change)),
                ]
            for change in usable:
                kind = unusual(clock, change)
                if kind and kinds.get(kind, 0) < 12 and change not in chosen:
                    kinds[kind] = kinds.get(kind, 0) + 1
                    chosen.append(change)
        instants, locals_ = set(), set()
        for change in set(chosen):
            some, others = vectors(clock, change, not arguments.all)
            instants.update(some)
            locals_.update(others)
        # Ordinary instants, away from a change or not, picked by a hash.
        for index in range(arguments.ordinary):
            digest = hashlib.sha1(f"{name} {index}".encode()).digest()
            instant = start + int.from_bytes(digest[:8], "big") % (end - start)
            if clock.whole_minutes(instant - 3 * DAY, instant + 3 * DAY):
                instants.add(instant)
                locals_.add(clock.local(instant))
        body = lines(clock, sorted(instants), sorted(locals_))
        if body:
            out.append(f"Z {name}")
            out += body
            count += len(body)
        for local in sorted(locals_)[: max(0, arguments.check - checked)][:3]:
            if clock.instant(local) != clock.instant_by_seconds(local):
                raise SystemExit(f"the reference is wrong for {name} {local}")
            checked += 1
    with open(arguments.target, "w", encoding="utf-8", newline="\n") as file:
        file.write("\n".join(out) + "\n")
    print(f"{count} vectors of {len(names)} zones, release {release}, {checked} checked")


if __name__ == "__main__":
    main()
