namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// STUB
public static class PureQLAggregates
{
    public static long? Sum(IEnumerable<long?> values)
    {
        return values.Sum();
    }

    public static decimal? Sum(IEnumerable<decimal?> values)
    {
        return values.Sum();
    }

    public static decimal? Average(IEnumerable<decimal?> values)
    {
        return values.Average();
    }

    public static DateOnly? Average(IEnumerable<DateOnly?> values)
    {
        throw new NotImplementedException();
    }

    public static TimeOnly? Average(IEnumerable<TimeOnly?> values)
    {
        throw new NotImplementedException();
    }

    public static DateTime? Average(IEnumerable<DateTime?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? Median(IEnumerable<decimal?> values)
    {
        throw new NotImplementedException();
    }

    public static DateOnly? Median(IEnumerable<DateOnly?> values)
    {
        throw new NotImplementedException();
    }

    public static TimeOnly? Median(IEnumerable<TimeOnly?> values)
    {
        throw new NotImplementedException();
    }

    public static DateTime? Median(IEnumerable<DateTime?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? PercentileContinuous(IEnumerable<decimal?> values, decimal fraction)
    {
        throw new NotImplementedException();
    }

    public static DateOnly? PercentileContinuous(IEnumerable<DateOnly?> values, decimal fraction)
    {
        throw new NotImplementedException();
    }

    public static TimeOnly? PercentileContinuous(IEnumerable<TimeOnly?> values, decimal fraction)
    {
        throw new NotImplementedException();
    }

    public static DateTime? PercentileContinuous(IEnumerable<DateTime?> values, decimal fraction)
    {
        throw new NotImplementedException();
    }

    public static T PercentileDiscrete<T>(IEnumerable<T> values, decimal fraction)
    {
        throw new NotImplementedException();
    }

    public static T Mode<T>(IEnumerable<T> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? VariancePopulation(IEnumerable<decimal?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? VarianceSample(IEnumerable<decimal?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? StandardDeviationPopulation(IEnumerable<decimal?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? StandardDeviationSample(IEnumerable<decimal?> values)
    {
        throw new NotImplementedException();
    }

    public static decimal? CovariancePopulation(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }

    public static decimal? CovarianceSample(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }

    public static decimal? Correlation(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }

    public static decimal? RegressionSlope(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }

    public static decimal? RegressionIntercept(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }

    public static decimal? RegressionRSquared(IEnumerable<(decimal?, decimal?)> pairs)
    {
        throw new NotImplementedException();
    }
}
