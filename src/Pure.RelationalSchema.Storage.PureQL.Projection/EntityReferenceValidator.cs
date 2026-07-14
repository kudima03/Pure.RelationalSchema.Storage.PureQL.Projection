using OneOf;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachDateTimeArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.EachTimeArithmetics;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using ModelEquality = PureQL.CSharp.Model.Equality;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Validates that every field reference in the query addresses a declared
// entity: the from entity, the from alias, or a join's entity. The spec
// gives joinItem no alias, so any other entity string (an undeclared join
// alias, a typo) is unresolvable; failing fast here prevents the silent
// bare-name fallback that would bind such references to the wrong column.
internal static class EntityReferenceValidator
{
    private static readonly IEnumerable<string> None = [];

    internal static void Validate(Query query)
    {
        HashSet<string> known = new(StringComparer.Ordinal) { query.From.Entity };

        if (query.From.Alias is not null)
        {
            _ = known.Add(query.From.Alias);
        }

        if (query.Join is not null)
        {
            foreach (Join join in query.Join)
            {
                _ = known.Add(join.Entity);
            }
        }

        string? unknown = Referenced(query).FirstOrDefault(entity =>
            !known.Contains(entity)
        );

        if (unknown is not null)
        {
            throw new NotSupportedException(
                $"Field reference entity '{unknown}' matches neither the "
                    + "from entity, the from alias, nor any join entity. "
                    + "Joined tables must be referenced by their full "
                    + "\"schema.table\" path (joins have no alias)."
            );
        }
    }

    private static IEnumerable<string> Referenced(Query query)
    {
        IEnumerable<string> entities = query.SelectExpressions.SelectMany(
            expression => expression.Match(
                single => Entities(single),
                array => Entities(array)
            )
        );

        if (query.Where is not null)
        {
            entities = entities.Concat(Entities(query.Where.Value));
        }

        if (query.Join is not null)
        {
            entities = entities.Concat(
                query.Join.SelectMany(join => Entities(join.On))
            );
        }

        if (query.GroupBy is not null)
        {
            entities = entities.Concat(query.GroupBy.SelectMany(Entities));
        }

        if (query.Having is not null)
        {
            entities = entities.Concat(Entities(query.Having));
        }

        if (query.OrderBy is not null)
        {
            entities = entities.Concat(
                query.OrderBy.SelectMany(item => Entities(item.Field))
            );
        }

        return entities;
    }

    private static IEnumerable<string> Entities(Field field)
    {
        return field.Match(
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity),
            f => One(f.Entity)
        );
    }

    private static IEnumerable<string> One(string entity)
    {
        return [entity];
    }

    // ===== Single-value returnings =====

    private static IEnumerable<string> Entities(SingleValueReturning returning)
    {
        return returning.Match(
            Entities,
            Entities,
            Entities,
            Entities,
            Entities,
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(BooleanReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            Entities,
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(DateReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            aggregate => aggregate.Match(
                max => Entities(max.Argument),
                min => Entities(min.Argument),
                average => Entities(average.Argument)
            )
        );
    }

    private static IEnumerable<string> Entities(DateTimeReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            aggregate => aggregate.Match(
                max => Entities(max.Argument),
                min => Entities(min.Argument),
                average => Entities(average.Argument)
            )
        );
    }

    private static IEnumerable<string> Entities(TimeReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            aggregate => aggregate.Match(
                max => Entities(max.Argument),
                min => Entities(min.Argument),
                average => Entities(average.Argument)
            )
        );
    }

    private static IEnumerable<string> Entities(StringReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            aggregate => aggregate.Match(
                max => Entities(max.Argument),
                min => Entities(min.Argument)
            )
        );
    }

    private static IEnumerable<string> Entities(NumberReturning returning)
    {
        return returning.Match(
            _ => None,
            _ => None,
            arithmetic => arithmetic.Match(
                add => add.Arguments.SelectMany(Entities),
                divide => divide.Arguments.SelectMany(Entities),
                multiply => multiply.Arguments.SelectMany(Entities),
                subtract => subtract.Arguments.SelectMany(Entities)
            ),
            aggregate => aggregate.Match(
                average => Entities(average.Argument),
                max => Entities(max.Argument),
                min => Entities(min.Argument),
                sum => Entities(sum.Argument)
            ),
            count => Entities(count.Argument)
        );
    }

    private static IEnumerable<string> Entities(UuidReturning returning)
    {
        return returning.Match(_ => None, _ => None);
    }

    // ===== Boolean composites =====

    private static IEnumerable<string> Entities(ModelEquality equality)
    {
        return equality.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(SingleValueEquality equality)
    {
        return equality.Match(
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right))
        );
    }

    private static IEnumerable<string> Entities(ArrayEquality equality)
    {
        return equality.Match(
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right))
        );
    }

    private static IEnumerable<string> Entities(BooleanOperator op)
    {
        return op.Match(
            and => and.Conditions.Match(
                conditions => conditions.SelectMany(Entities),
                array => Entities(array)
            ),
            or => or.Conditions.Match(
                conditions => conditions.SelectMany(Entities),
                array => Entities(array)
            ),
            not => Entities(not.Condition)
        );
    }

    private static IEnumerable<string> Entities(Comparison comparison)
    {
        return comparison.Match(
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right))
        );
    }

    // ===== Array returnings =====

    private static IEnumerable<string> Entities(ArrayReturning returning)
    {
        return returning.Match(
            Entities,
            Entities,
            Entities,
            Entities,
            Entities,
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(BooleanArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None,
            Entities,
            Entities,
            and => and.Conditions.SelectMany(Entities),
            or => or.Conditions.SelectMany(Entities),
            not => Entities(not.Condition)
        );
    }

    private static IEnumerable<string> Entities(DateArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None,
            Entities
        );
    }

    private static IEnumerable<string> Entities(DateTimeArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None,
            Entities
        );
    }

    private static IEnumerable<string> Entities(NumberArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None,
            Entities,
            Entities,
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(StringArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None
        );
    }

    private static IEnumerable<string> Entities(TimeArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None,
            Entities
        );
    }

    private static IEnumerable<string> Entities(UuidArrayReturning returning)
    {
        return returning.Match(
            _ => None,
            field => One(field.Entity),
            _ => None
        );
    }

    // ===== Per-row composites =====

    private static IEnumerable<string> Entities(EachComparison comparison)
    {
        return comparison.Match(
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right)),
            c => Entities(c.Left).Concat(Entities(c.Right))
        );
    }

    private static IEnumerable<string> Entities(EachEquality equality)
    {
        return equality.Match(
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right)),
            eq => Entities(eq.Left).Concat(Entities(eq.Right))
        );
    }

    private static IEnumerable<string> Entities(EachArithmetic arithmetic)
    {
        return arithmetic.Match(
            add => add.Values.SelectMany(Entities),
            subtract => subtract.Values.SelectMany(Entities),
            multiply => multiply.Values.SelectMany(Entities),
            divide => divide.Values.SelectMany(Entities)
        );
    }

    private static IEnumerable<string> Entities(EachDateAddDays op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    private static IEnumerable<string> Entities(EachDateDiffDays op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    private static IEnumerable<string> Entities(EachTimeAddSeconds op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    private static IEnumerable<string> Entities(EachTimeDiffSeconds op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    private static IEnumerable<string> Entities(EachDateTimeAddSeconds op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    private static IEnumerable<string> Entities(EachDateTimeDiffSeconds op)
    {
        return Entities(op.Left).Concat(Entities(op.Right));
    }

    // ===== Broadcast operands (single value | per-row array) =====

    private static IEnumerable<string> Entities(
        OneOf<BooleanReturning, BooleanArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<NumberReturning, NumberArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<StringReturning, StringArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<DateReturning, DateArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<TimeReturning, TimeArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<DateTimeReturning, DateTimeArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }

    private static IEnumerable<string> Entities(
        OneOf<UuidReturning, UuidArrayReturning> value
    )
    {
        return value.Match(
            Entities,
            Entities
        );
    }
}
