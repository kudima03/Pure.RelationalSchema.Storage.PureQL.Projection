namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The frame of a window: which rows of the ordered partition of a row its
// window holds. The current row is always in its frame.
// - Partition: every row of the partition (a window without a frame).
// - Rows(preceding, following): the row, up to `preceding` rows before it
//   and up to `following` rows after it; null is every row on that side, 0
//   none.
// - Range(descending, preceding, unit, following, unit): the rows whose sort
//   key, the window's one, lies within the distances of the row's key, both
//   bounds included; null is every row on that side, 0 only the rows that
//   share the key. With a descending key, preceding reaches towards greater
//   values. A row whose key is null has the rows whose key is null.
// A count or a distance below 0 is an ArgumentOutOfRangeException.
public sealed class PureQLFrame
{
    private PureQLFrame(
        bool rows,
        bool range,
        bool descending,
        decimal? preceding,
        PureQLRangeUnit precedingUnit,
        decimal? following,
        PureQLRangeUnit followingUnit
    )
    {
        IsRows = rows;
        IsRange = range;
        Descending = descending;
        Preceding = preceding;
        PrecedingUnit = precedingUnit;
        Following = following;
        FollowingUnit = followingUnit;
    }

    public static PureQLFrame Partition { get; } =
        new(false, false, false, null, PureQLRangeUnit.Number, null, PureQLRangeUnit.Number);

    public bool IsRows { get; }

    public bool IsRange { get; }

    public bool Descending { get; }

    public decimal? Preceding { get; }

    public PureQLRangeUnit PrecedingUnit { get; }

    public decimal? Following { get; }

    public PureQLRangeUnit FollowingUnit { get; }

    public static PureQLFrame Rows(long? preceding, long? following)
    {
        return new PureQLFrame(
            true,
            false,
            false,
            Checked(preceding, nameof(preceding)),
            PureQLRangeUnit.Number,
            Checked(following, nameof(following)),
            PureQLRangeUnit.Number
        );
    }

    public static PureQLFrame Range(
        bool descending,
        decimal? preceding,
        PureQLRangeUnit precedingUnit,
        decimal? following,
        PureQLRangeUnit followingUnit
    )
    {
        return new PureQLFrame(
            false,
            true,
            descending,
            Checked(preceding, nameof(preceding)),
            precedingUnit,
            Checked(following, nameof(following)),
            followingUnit
        );
    }

    private static decimal? Checked(decimal? distance, string side)
    {
        return distance < 0
            ? throw new ArgumentOutOfRangeException(
                side,
                distance,
                "A frame count or a range distance is 0 or more."
            )
            : distance;
    }
}
