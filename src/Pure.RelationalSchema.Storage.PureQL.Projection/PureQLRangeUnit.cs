namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The unit a distance of a range window is written in: a number for an
// integer or decimal sort key, days or months for a date, seconds or months
// for a datetime.
public enum PureQLRangeUnit
{
    Number,
    Days,
    Months,
    Seconds,
}
