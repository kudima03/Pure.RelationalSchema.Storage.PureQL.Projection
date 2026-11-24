using System.Linq.Expressions;
using System.Reflection;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// String operators. A string is a sequence of code points: lengths and
// positions count code points from 1, and nothing depends on a locale. The
// lifted ones are null as soon as an operand is null; contains, startsWith,
// endsWith and matchesRegex are conditions, false on null; concatWith skips
// its null operands; extractRegex is null when nothing matches. A regular
// expression and its group are a literal or a parameter, never computed.
internal static class Text
{
    private static readonly MethodInfo ConcatMethod = Functions.Method(
        typeof(string),
        nameof(string.Concat),
        typeof(string),
        typeof(string)
    );

    public static Expression Concat(object node, Scope scope)
    {
        return Lifted.All(
            [.. ModelNode.Children(node, "Values").Select(scope.Compile)],
            typeof(string),
            values => values.Aggregate(
                (left, right) => Expression.Call(ConcatMethod, left, right)
            )
        );
    }

    public static Expression ConcatWith(object node, Scope scope)
    {
        return Functions.Call(
            typeof(PureQLText),
            nameof(PureQLText.ConcatWith),
            scope.Compile(ModelNode.Child(node, "Separator")),
            Strings(node, scope, "Values")
        );
    }

    public static Expression Length(object node, Scope scope)
    {
        return Function(node, scope, typeof(long?), nameof(PureQLText.Length), "Operand");
    }

    public static Expression Upper(object node, Scope scope)
    {
        return Function(node, scope, typeof(string), nameof(PureQLText.Upper), "Operand");
    }

    public static Expression Lower(object node, Scope scope)
    {
        return Function(node, scope, typeof(string), nameof(PureQLText.Lower), "Operand");
    }

    public static Expression FoldCase(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.FoldCase),
            "Operand"
        );
    }

    // The operand in a Unicode normalization form, which is string.Normalize.
    public static Expression Normalize(object node, Scope scope)
    {
        System.Text.NormalizationForm form = (NormalizationForm)
            ModelNode.Child(node, "Form") switch
        {
            NormalizationForm.NFD => System.Text.NormalizationForm.FormD,
            NormalizationForm.NFKC => System.Text.NormalizationForm.FormKC,
            NormalizationForm.NFKD => System.Text.NormalizationForm.FormKD,
            NormalizationForm.NFC or _ => System.Text.NormalizationForm.FormC,
        };

        return Lifted.Unary(
            scope.Compile(ModelNode.Child(node, "Operand")),
            typeof(string),
            value => Functions.Call(
                value,
                nameof(string.Normalize),
                Expression.Constant(form)
            )
        );
    }

    public static Expression RemoveDiacritics(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.RemoveDiacritics),
            "Operand"
        );
    }

    public static Expression Trim(object node, Scope scope)
    {
        return Trimmed(node, scope, nameof(PureQLText.Trim));
    }

    public static Expression TrimStart(object node, Scope scope)
    {
        return Trimmed(node, scope, nameof(PureQLText.TrimStart));
    }

    public static Expression TrimEnd(object node, Scope scope)
    {
        return Trimmed(node, scope, nameof(PureQLText.TrimEnd));
    }

    public static Expression Substring(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.Substring),
            "Operand",
            "Start",
            "Length"
        );
    }

    public static Expression Replace(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.Replace),
            "Operand",
            "Pattern",
            "Replacement"
        );
    }

    public static Expression Position(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(long?),
            nameof(PureQLText.Position),
            "Operand",
            "Pattern",
            "Start"
        );
    }

    public static Expression SplitPart(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.SplitPart),
            "Operand",
            "Delimiter",
            "Index"
        );
    }

    public static Expression PadStart(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.PadStart),
            "Operand",
            "Length",
            "Fill"
        );
    }

    public static Expression PadEnd(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.PadEnd),
            "Operand",
            "Length",
            "Fill"
        );
    }

    public static Expression Repeat(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.Repeat),
            "Operand",
            "Count"
        );
    }

    public static Expression Reverse(object node, Scope scope)
    {
        return Function(
            node,
            scope,
            typeof(string),
            nameof(PureQLText.Reverse),
            "Operand"
        );
    }

    // The code points of the pattern occur in the operand as one run:
    // string.Contains, which compares ordinally.
    public static Expression Contains(object node, Scope scope)
    {
        return Matched(
            node,
            scope,
            (operand, pattern) => Functions.Call(
                operand,
                nameof(string.Contains),
                pattern
            )
        );
    }

    public static Expression StartsWith(object node, Scope scope)
    {
        return Matched(
            node,
            scope,
            (operand, pattern) => Functions.Call(
                operand,
                nameof(string.StartsWith),
                pattern,
                Expression.Constant(StringComparison.Ordinal)
            )
        );
    }

    public static Expression EndsWith(object node, Scope scope)
    {
        return Matched(
            node,
            scope,
            (operand, pattern) => Functions.Call(
                operand,
                nameof(string.EndsWith),
                pattern,
                Expression.Constant(StringComparison.Ordinal)
            )
        );
    }

    public static Expression MatchesRegex(object node, Scope scope)
    {
        Expression operand = scope.Compile(ModelNode.Child(node, "Operand"));

        return Expression.AndAlso(
            Expression.Not(Typed.IsNull(operand)),
            Functions.Call(
                typeof(PureQLText),
                nameof(PureQLText.MatchesRegex),
                operand,
                scope.Compile(ModelNode.Child(node, "Pattern"))
            )
        );
    }

    public static Expression ExtractRegex(object node, Scope scope)
    {
        Expression operand = scope.Compile(ModelNode.Child(node, "Operand"));
        Expression pattern = scope.Compile(ModelNode.Child(node, "Pattern"));

        return Expression.Condition(
            Typed.IsNull(operand),
            Typed.Null(typeof(string)),
            ModelNode.Optional(node, "Group") is object group
                ? Functions.Call(
                    typeof(PureQLText),
                    nameof(PureQLText.ExtractRegex),
                    operand,
                    pattern,
                    Typed.As(scope.Compile(group), typeof(long))
                )
                : Functions.Call(
                    typeof(PureQLText),
                    nameof(PureQLText.ExtractRegex),
                    operand,
                    pattern
                )
        );
    }

    public static Expression ReplaceRegex(object node, Scope scope)
    {
        Expression pattern = scope.Compile(ModelNode.Child(node, "Pattern"));

        return Lifted.Binary(
            scope.Compile(ModelNode.Child(node, "Operand")),
            scope.Compile(ModelNode.Child(node, "Replacement")),
            typeof(string),
            (operand, replacement) => Functions.Call(
                typeof(PureQLText),
                nameof(PureQLText.ReplaceRegex),
                operand,
                pattern,
                replacement
            )
        );
    }

    // A function of PureQLText, lifted over the operands the node has: a
    // string is passed as it is, an integer as a long.
    private static Expression Function(
        object node,
        Scope scope,
        Type result,
        string name,
        params string[] operands
    )
    {
        return Lifted.All(
            [
                .. operands
                    .Select(operand => ModelNode.Optional(node, operand))
                    .OfType<object>()
                    .Select(scope.Compile),
            ],
            result,
            values => Functions.Call(typeof(PureQLText), name, [.. values])
        );
    }

    private static Expression Trimmed(object node, Scope scope, string name)
    {
        Expression removables = Strings(node, scope, "Removables");

        return Lifted.Unary(
            scope.Compile(ModelNode.Child(node, "Operand")),
            typeof(string),
            value => Functions.Call(typeof(PureQLText), name, value, removables)
        );
    }

    // A condition on two strings that is false when either is null.
    private static Expression Matched(
        object node,
        Scope scope,
        Func<Expression, Expression, Expression> match
    )
    {
        Expression operand = scope.Compile(ModelNode.Child(node, "Operand"));
        Expression pattern = scope.Compile(ModelNode.Child(node, "Pattern"));

        return Expression.AndAlso(
            Expression.AndAlso(
                Expression.Not(Typed.IsNull(operand)),
                Expression.Not(Typed.IsNull(pattern))
            ),
            match(operand, pattern)
        );
    }

    private static Expression Strings(object node, Scope scope, string property)
    {
        return Expression.NewArrayInit(
            typeof(string),
            ModelNode.Children(node, property).Select(scope.Compile)
        );
    }
}
