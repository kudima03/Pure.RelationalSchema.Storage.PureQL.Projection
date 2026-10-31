namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A clock at an instant a test sets, counting how often it is read.
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public int Reads { get; private set; }

    public static FixedClock At(string instant)
    {
        return new FixedClock(DateTimeOffset.Parse(instant, null));
    }

    public override DateTimeOffset GetUtcNow()
    {
        Reads++;
        return Now;
    }
}
