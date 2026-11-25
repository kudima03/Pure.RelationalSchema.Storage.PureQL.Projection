using System.Collections.Concurrent;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The clocks of a time zone: the local date and time of an instant, and the
// instant of a local date and time. Both come from one lookup, the offset from
// UTC a zone has at an instant, so they hold for every zone and every change
// of its clocks: forward, back, across midnight, by any amount. A local date
// and time is carried as a DateTime whose ticks count from midnight of
// 0001-01-01 on the local clock.
internal static class ZoneClock
{
    // The name that reads an instant as no zone does: every function gives what
    // it gives without a zone.
    private const string Utc = "UTC";

    // No zone is, or has been, further from UTC than this: local mean time
    // stays within 15:56, and TimeZoneInfo holds no offset beyond 14 hours.
    private const long Widest = 16 * TimeSpan.TicksPerHour;

    // How far apart the walk near a change of the clocks reads offsets. An
    // offset stays in force for days at the least (95 hours, in
    // Africa/Freetown in 1939, is the shortest in release 2026e of the
    // database), so no change hides between two readings.
    private const long Step = TimeSpan.TicksPerHour;

    private static readonly ConcurrentDictionary<string, TimeZoneInfo> Zones = new(
        StringComparer.Ordinal
    );

    // Whether a text names a time zone: a name of the specification's list, or
    // UTC. A name is matched exactly, letter case included.
    public static bool Names(string name)
    {
        return name == Utc || TimeZoneNames.All.Contains(name);
    }

    // The zone a name stands for. Any other text is an execution error: a zone
    // that does not exist, an older name kept as a link, an abbreviation, an
    // offset, the empty string.
    public static TimeZoneInfo Of(string zone)
    {
        return Names(zone)
            ? Zones.GetOrAdd(zone, Found)
            : throw new TimeZoneNotFoundException(
                $"'{zone}' is not the name of a time zone."
            );
    }

    // The local date and time of an instant: the instant shifted by the offset
    // the zone has at that instant. A local date outside the years 0001-9999
    // is an execution error.
    public static DateTime Local(DateTime instant, TimeZoneInfo zone)
    {
        long ticks = instant.Ticks + Offset(zone, instant.Ticks);

        return InRange(ticks)
            ? new DateTime(ticks, DateTimeKind.Unspecified)
            : throw new ArgumentOutOfRangeException(
                nameof(instant),
                "The local date of the instant is outside the years 0001-9999."
            );
    }

    // The instant of a local date and time: the first instant at which the
    // local date and time are the given ones or later. An instant outside the
    // years 0001-9999 is an execution error.
    public static DateTime Instant(DateTime local, TimeZoneInfo zone)
    {
        long ticks = InstantTicks(local.Ticks, zone);

        return InRange(ticks)
            ? new DateTime(ticks, DateTimeKind.Utc)
            : throw new ArgumentOutOfRangeException(
                nameof(local),
                "The instant of the local date and time is outside the years 0001-9999."
            );
    }

    // The ticks of the first instant at which the local date and time are the
    // given ones or later; they may fall outside the range of a datetime. A
    // time that occurs twice gives its first occurrence, and a time that never
    // occurs the instant the clocks jump over it.
    //
    // Away from a change of the clocks three offsets settle it. The offset at
    // the local time, read as if it were an instant, gives a candidate: the
    // local time less that offset. When the offset is the same there, the
    // clocks read the local time at the candidate. An earlier instant could
    // only have read as much under a larger offset, and no offset exceeds
    // Widest, so it would be at most Widest less the offset before the
    // candidate, 32 hours at the very most. When the offset is the same that
    // far back as well, it did not change in between, since an offset stays
    // in force for longer than that (see Step), and the candidate is the
    // first. Otherwise a change is near, and the walk below finds the answer.
    public static long InstantTicks(long local, TimeZoneInfo zone)
    {
        long offset = Offset(zone, local);
        long instant = local - offset;

        return
            Offset(zone, instant) == offset
            && Offset(zone, instant - (Widest - offset)) == offset
            ? instant
            : Searched(local, zone);
    }

    // The same instant near a change of the clocks. No instant earlier than
    // the local time less Widest has reached it, so the walk starts there and
    // goes forward through each stretch of one offset. A stretch reaches the
    // local time at the local time less its offset, or at its own start when
    // the clocks were already past it there; the answer is that instant in
    // the first stretch that is still in force at it. The offset is read up
    // to that instant and no further, so a change found on the way ends the
    // stretch before it.
    private static long Searched(long local, TimeZoneInfo zone)
    {
        long start = local - Widest;
        long offset = Offset(zone, start);
        long read = start;

        while (true)
        {
            long instant = Math.Max(start, local - offset);

            if (instant <= read)
            {
                return instant;
            }

            long ahead = Math.Min(read + Step, instant);
            long next = Offset(zone, ahead);

            if (next != offset)
            {
                start = Change(zone, read, ahead, offset);
                offset = next;
            }

            read = ahead;
        }
    }

    // The first instant after `before`, and not after `after`, at which the
    // offset is no longer the one in force at `before`.
    private static long Change(TimeZoneInfo zone, long before, long after, long offset)
    {
        while (after - before > 1)
        {
            long middle = before + ((after - before) / 2);

            if (Offset(zone, middle) == offset)
            {
                before = middle;
            }
            else
            {
                after = middle;
            }
        }

        return after;
    }

    // The offset from UTC a zone has at an instant, in ticks. Before the first
    // instant of a datetime and after the last one it is the offset the zone
    // has there.
    private static long Offset(TimeZoneInfo zone, long instant)
    {
        DateTime read = new(
            Math.Clamp(instant, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks),
            DateTimeKind.Utc
        );

        return zone.GetUtcOffset(read).Ticks;
    }

    private static bool InRange(long ticks)
    {
        return ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks;
    }

    private static TimeZoneInfo Found(string name)
    {
        return name == Utc ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(name);
    }
}
