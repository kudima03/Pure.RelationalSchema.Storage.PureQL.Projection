using System.Globalization;
using System.Text;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A pattern of the regular expression dialect of the specification, read
// once and written as a .NET regular expression that matches by code point:
// literals, escapes of the special characters, the dot, classes, the ASCII
// sets \d \w \s and their negations, \t \n \r, the anchors ^ and $, the word
// boundary \b and \B, alternatives, capturing and (?: ) groups, and the
// quantifiers * + ? {n} {n,} {n,m}, greedy or lazy. Anything else is not a
// pattern, as are a line feed written as itself, parentheses that do not
// balance, a range or a {n,m} out of order and a quantifier with nothing to
// repeat: a FormatException.
internal sealed class RegexTranslation
{
    private const string Special = @"\()[]{}*+?|^$./";

    private const string WordCharacter = "[0-9A-Z_a-z]";

    // \b: a word character on exactly one side.
    private const string Boundary =
        "(?:(?<="
        + WordCharacter
        + ")(?!"
        + WordCharacter
        + ")|(?<!"
        + WordCharacter
        + ")(?="
        + WordCharacter
        + "))";

    // \B: a word character on both sides or on neither.
    private const string NotBoundary =
        "(?:(?<="
        + WordCharacter
        + ")(?="
        + WordCharacter
        + ")|(?<!"
        + WordCharacter
        + ")(?!"
        + WordCharacter
        + "))";

    private readonly string _pattern;

    private int _index;

    public RegexTranslation(string pattern)
    {
        if (pattern.Contains('\n'))
        {
            throw new FormatException(
                "A line feed in a regular expression is written with a backslash and n."
            );
        }

        _pattern = pattern;
        Expression = Translated();
    }

    // The pattern as a .NET regular expression. Each construct takes whole
    // code points, so a match that starts where a code point starts also
    // ends where one ends.
    public string Expression { get; }

    // The number of capturing groups, numbered from 1 by their opening
    // parentheses.
    public int Groups { get; private set; }

    private string Translated()
    {
        StringBuilder translated = new();
        int open = 0;
        bool repeatable = false;

        while (_index < _pattern.Length)
        {
            char next = _pattern[_index];

            if (next is '*' or '+' or '?' or '{')
            {
                if (!repeatable)
                {
                    throw NotAPattern("a quantifier has nothing to repeat");
                }

                _ = translated.Append(Quantifier());
                repeatable = false;
            }
            else if (next == '(')
            {
                _ = translated.Append(GroupStart());
                open++;
                repeatable = false;
            }
            else if (next == ')')
            {
                if (open == 0)
                {
                    throw NotAPattern("a parenthesis closes no group");
                }

                _ = translated.Append(')');
                _index++;
                open--;
                repeatable = true;
            }
            else if (next is '|' or '^' or '$' || IsBoundary())
            {
                _ = translated.Append(Assertion());
                repeatable = false;
            }
            else
            {
                _ = translated.Append(Atom());
                repeatable = true;
            }
        }

        return open == 0
            ? translated.ToString()
            : throw NotAPattern("a group is not closed");
    }

    private bool IsBoundary()
    {
        return _pattern[_index] == '\\'
            && _index + 1 < _pattern.Length
            && _pattern[_index + 1] is 'b' or 'B';
    }

    // An alternative, an anchor or a word boundary: what matches no code
    // point, so no quantifier may follow it.
    private string Assertion()
    {
        char symbol = _pattern[_index];

        _index++;

        if (symbol == '\\')
        {
            _index++;

            return _pattern[_index - 1] == 'b' ? Boundary : NotBoundary;
        }

        return symbol switch
        {
            '^' => @"\A",
            '$' => @"\z",
            _ => "|",
        };
    }

    // ( opens a capturing group and (?: a group that does not capture;
    // nothing else may follow (?.
    private string GroupStart()
    {
        _index++;

        if (_index == _pattern.Length || _pattern[_index] != '?')
        {
            Groups++;

            return "(";
        }

        if (_index + 1 == _pattern.Length || _pattern[_index + 1] != ':')
        {
            throw NotAPattern("a group starts with ? and is not (?: )");
        }

        _index += 2;

        return "(?:";
    }

    // * + ? {n} {n,} {n,m}, each greedy, or lazy with one ? after it.
    private string Quantifier()
    {
        string quantifier;

        if (_pattern[_index] == '{')
        {
            quantifier = Counts();
        }
        else
        {
            quantifier = _pattern[_index].ToString();
            _index++;
        }

        if (_index < _pattern.Length && _pattern[_index] == '?')
        {
            _index++;
            quantifier += "?";
        }

        return quantifier;
    }

    private string Counts()
    {
        _index++;

        int least = Count();
        string counts = "{" + least.ToString(CultureInfo.InvariantCulture);

        if (_index < _pattern.Length && _pattern[_index] == ',')
        {
            _index++;
            counts += ",";

            if (_index < _pattern.Length && char.IsAsciiDigit(_pattern[_index]))
            {
                int most = Count();

                if (most < least)
                {
                    throw NotAPattern("the counts of a quantifier are out of order");
                }

                counts += most.ToString(CultureInfo.InvariantCulture);
            }
        }

        if (_index == _pattern.Length || _pattern[_index] != '}')
        {
            throw NotAPattern("a { starts no {n}, {n,} or {n,m}");
        }

        _index++;

        return counts + "}";
    }

    private int Count()
    {
        int start = _index;

        while (_index < _pattern.Length && char.IsAsciiDigit(_pattern[_index]))
        {
            _index++;
        }

        return _index == start ? throw NotAPattern("a { starts no {n}, {n,} or {n,m}")
            : int.TryParse(
                _pattern.AsSpan(start, _index - start),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int count
            )
                ? count
            : throw NotAPattern("a count of a quantifier is too large");
    }

    // What matches one code point: a literal, an escape, the dot or a class.
    private string Atom()
    {
        char symbol = _pattern[_index];

        if (symbol == '[')
        {
            return Class();
        }

        if (symbol == '.')
        {
            _index++;

            return CodePointSet.NotLineFeed.Expression();
        }

        if (symbol is ']' or '}')
        {
            throw NotAPattern($"a {symbol} is written with a backslash");
        }

        if (symbol != '\\')
        {
            return Literal(CodePoint(false));
        }

        _index++;

        return Escaped(false) is CodePointSet set
            ? set.Expression()
            : Literal(CodePoint(true));
    }

    // The set of an escape that stands for one (\d \D \w \W \s \S), with the
    // index after it, or null with the index at the code point an escape
    // stands for (a special character, or the letter of \t \n \r).
    private CodePointSet? Escaped(bool inClass)
    {
        if (_index == _pattern.Length)
        {
            throw NotAPattern("the pattern ends with a backslash");
        }

        char symbol = _pattern[_index];
        CodePointSet? set = symbol switch
        {
            'd' => CodePointSet.Digits,
            'D' => CodePointSet.Digits.Inverse(),
            'w' => CodePointSet.WordCharacters,
            'W' => CodePointSet.WordCharacters.Inverse(),
            's' => CodePointSet.WhiteSpace,
            'S' => CodePointSet.WhiteSpace.Inverse(),
            _ => null,
        };

        if (set is not null)
        {
            _index++;
        }
        else if (
            symbol is not ('t' or 'n' or 'r')
            && !Special.Contains(symbol, StringComparison.Ordinal)
            && !(inClass && symbol == '-')
        )
        {
            throw NotAPattern("a backslash is before a character that needs none");
        }

        return set;
    }

    // The code point at the index, which is then after it. After a backslash
    // the letters t, n and r are a tab, a line feed and a carriage return.
    private int CodePoint(bool escaped)
    {
        int width = Utf16.Width(_pattern, _index);
        int codePoint = Utf16.CodePoint(_pattern, _index, width);

        _index += width;

        return !escaped ? codePoint
            : codePoint == 't' ? '\t'
            : codePoint == 'n' ? '\n'
            : codePoint == 'r' ? '\r'
            : codePoint;
    }

    // [ ... ] or [^ ... ]: code points, ranges of them in code point order
    // and the escapes that stand for a set. A hyphen stands for itself when
    // it is first, last or escaped, and is the middle of a range otherwise.
    private string Class()
    {
        CodePointSet set = new();
        bool negated = _index + 1 < _pattern.Length && _pattern[_index + 1] == '^';
        bool empty = true;

        _index += negated ? 2 : 1;

        while (_index < _pattern.Length && _pattern[_index] != ']')
        {
            int? first = Member(set, empty);

            empty = false;

            if (first is int lowest)
            {
                _ = set.With(lowest, RangeEnd(set, lowest));
            }
        }

        if (_index == _pattern.Length)
        {
            throw NotAPattern("a class is not closed");
        }

        if (empty)
        {
            throw NotAPattern("a class is empty");
        }

        _index++;

        return (negated ? set.Inverse() : set).Expression();
    }

    // One member of a class: a code point, returned, or an escape that stands
    // for a set, added to the set of the class. A hyphen is a member only as
    // the first or the last one.
    private int? Member(CodePointSet set, bool first)
    {
        char symbol = _pattern[_index];

        if (symbol == '\\')
        {
            _index++;

            if (Escaped(true) is CodePointSet escaped)
            {
                _ = set.With(escaped);

                return null;
            }

            return CodePoint(true);
        }

        return symbol is '[' or '^'
                ? throw NotAPattern($"a {symbol} in a class is written with a backslash")
            : symbol == '-' && !first && !Closes(_index + 1)
                ? throw NotAPattern("a hyphen in a class is not first, last or escaped")
            : CodePoint(false);
    }

    // The last code point of the range that starts with first: after a
    // hyphen that is not the last member, the member that follows it, and
    // first itself otherwise.
    private int RangeEnd(CodePointSet set, int first)
    {
        if (_index == _pattern.Length || _pattern[_index] != '-' || Closes(_index + 1))
        {
            return first;
        }

        _index++;

        int last =
            _index < _pattern.Length && Member(set, false) is int member
                ? member
                : throw NotAPattern("a range of a class does not end with a code point");

        return last >= first
            ? last
            : throw NotAPattern("a range of a class is out of order");
    }

    private bool Closes(int index)
    {
        return index < _pattern.Length && _pattern[index] == ']';
    }

    // One code point of the pattern that stands for itself. A surrogate pair
    // is taken whole, and a high surrogate without its pair matches only one
    // that no low surrogate follows.
    private static string Literal(int codePoint)
    {
        return new CodePointSet().With(codePoint, codePoint).Expression();
    }

    private FormatException NotAPattern(string reason)
    {
        int read = Math.Min(_index, _pattern.Length);
        long position = Utf16.Count(_pattern.AsSpan(0, read));

        return new FormatException(
            $"'{_pattern}' is not a regular expression of the dialect: "
                + $"{reason}, at code point {position + 1}."
        );
    }
}
