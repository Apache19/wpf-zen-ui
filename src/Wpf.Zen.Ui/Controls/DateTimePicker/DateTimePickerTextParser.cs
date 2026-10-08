// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Text;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Recognizes date/time fragments using explicit formats, standard patterns, and field ranges, and
/// fills missing fields.
/// </summary>
internal static class DateTimePickerTextParser
{
    /// <summary>
    /// Attempts exact parsing first, then uses separators and numeric ranges to recognize
    /// fragments. Ambiguous input fails parsing.
    /// Missing years use the current year; time-only input uses today's date; other fields use
    /// their minimum initial values.
    /// </summary>
    /// <param name="text">The complete text or date/time fragment to parse.</param>
    /// <param name="format">The display format used for exact parsing and field ordering.</param>
    /// <param name="culture">
    /// The culture supplying patterns, separators, and calendar rules.
    /// </param>
    /// <param name="value">The parsed value when parsing succeeds.</param>
    /// <returns>
    /// True when the input identifies a valid date/time without ambiguous fields.
    /// </returns>
    internal static bool TryParse(string text, string format, CultureInfo culture, out DateTime value)
    {
        var exact = DateTime.TryParseExact(
            text, format, culture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
            out value
        );

        if (exact)
        {
            return true;
        }

        var input = text.Trim();
        var now   = DateTime.Now;

        // An isolated number must exclude every month, day, hour, minute, and second
        // interpretation. Two-digit year windows are not used.
        if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            if (input.Length > 4 || number <= 59)
            {
                return false;
            }

            var yearFields = new Dictionary<char, int>
            {
                ['y'] = number
            };

            return TryCreate(yearFields, culture, now, out value);
        }

        // The explicit format determines field order first, followed by ISO and culture-specific
        // date/time patterns.
        var formats = new List<string>();
        if (format.Length == 1)
        {
            formats.AddRange(culture.DateTimeFormat.GetAllDateTimePatterns(format[0]));
        }
        else
        {
            formats.Add(format);
        }

        formats.Add("yyyy-MM-dd HH:mm:ss");
        formats.Add("yyyy-MM-dd'T'HH:mm:ss");
        formats.Add(culture.DateTimeFormat.ShortDatePattern + " HH:mm:ss");
        formats.Add("HH:mm:ss");

        foreach (var pattern in formats)
        {
            if (!TryTokenize(pattern, culture.DateTimeFormat, out var tokens))
            {
                continue;
            }

            if (TryPattern(input, tokens, culture, now, out value))
            {
                return true;
            }

            // Remove the year and its adjacent separator, retaining the format's month/day order
            // and any following time fields.
            var yearIndex = tokens.FindIndex(token => token.Field == 'y');
            if (yearIndex >= 0)
            {
                var withoutYear    = new List<FormatToken>(tokens);
                var separatorIndex = yearIndex == 0 ? yearIndex + 1 : yearIndex - 1;
                if (separatorIndex < withoutYear.Count)
                {
                    withoutYear.RemoveAt(Math.Max(yearIndex, separatorIndex));
                    withoutYear.RemoveAt(Math.Min(yearIndex, separatorIndex));
                    if (TryPattern(input, withoutYear, culture, now, out value))
                    {
                        return true;
                    }
                }
            }

            var hourIndex = tokens.FindIndex(token => token.Field == 'H');
            if (hourIndex > 0)
            {
                var timeTokens = tokens.GetRange(hourIndex, tokens.Count - hourIndex);
                if (TryPattern(input, timeTokens, culture, now, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Matches separated field prefixes, requiring at least two fields or a field with a literal
    /// unit to disambiguate isolated numbers.
    /// </summary>
    private static bool TryPattern(
        string input, List<FormatToken> tokens, CultureInfo culture, DateTime now, out DateTime value)
    {
        value = default;

        var position   = 0;
        var hasLiteral = false;
        var fields     = new Dictionary<char, int>();

        for (var index = 0; index < tokens.Count && position < input.Length; index++)
        {
            var token = tokens[index];
            if (token.Field == '\0')
            {
                var literal      = token.Literal;
                var finalLiteral = literal.TrimEnd();
                var remainder    = input.Substring(position);
                if (string.Equals(remainder, finalLiteral, StringComparison.Ordinal))
                {
                    literal = finalLiteral;
                }

                if (input.Length - position < literal.Length
                    || string.CompareOrdinal(input, position, literal, 0, literal.Length) != 0)
                {
                    return false;
                }

                position   += literal.Length;
                hasLiteral =  true;

                if (position == input.Length && index + 1 < tokens.Count
                    && (literal.Length == 0 || !char.IsLetter(literal[literal.Length - 1])))
                {
                    return false;
                }

                continue;
            }

            var start = position;

            while (position < input.Length && input[position] >= '0' && input[position] <= '9')
            {
                position++;
            }

            var length        = position - start;
            var maximumLength = token.Field == 'y' ? 4 : 2;
            if (length == 0 || length > maximumLength)
            {
                return false;
            }

            var digits = input.Substring(start, length);
            var number = int.Parse(digits, CultureInfo.InvariantCulture);
            if (token.Field == 'y' && length < 4 && number <= 59)
            {
                return false;
            }

            fields[token.Field] = number;
        }

        if (position != input.Length || fields.Count == 0 || (fields.Count == 1 && !hasLiteral))
        {
            return false;
        }

        return TryCreate(fields, culture, now, out value);
    }

    /// <summary>
    /// Month/day input uses the current year; time-only input uses today's date; other missing
    /// fields use their minimum initial values.
    /// Constructs the result with the culture's calendar to validate leap days, ranges, and date
    /// combinations consistently.
    /// </summary>
    private static bool TryCreate(
        Dictionary<char, int> fields, CultureInfo culture, DateTime now, out DateTime value)
    {
        value = default;

        var calendar     = culture.DateTimeFormat.Calendar;
        var timeOnly     = !fields.ContainsKey('y') && !fields.ContainsKey('M') && !fields.ContainsKey('d');
        var year         = GetField(fields, 'y', calendar.GetYear(now));
        var defaultMonth = timeOnly ? calendar.GetMonth(now) : 1;
        var defaultDay   = timeOnly ? calendar.GetDayOfMonth(now) : 1;
        var month        = GetField(fields, 'M', defaultMonth);
        var day          = GetField(fields, 'd', defaultDay);
        var hour         = GetField(fields, 'H', 0);
        var minute       = GetField(fields, 'm', 0);
        var second       = GetField(fields, 's', 0);

        try
        {
            value = calendar.ToDateTime(year, month, day, hour, minute, second, 0);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a recognized field or its completion default.
    /// </summary>
    private static int GetField(Dictionary<char, int> fields, char field, int defaultValue)
    {
        return fields.TryGetValue(field, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Recognizes numeric fields and .NET format literals, escapes, and culture-specific
    /// separators.
    /// Adjacent numeric fields, repeated fields, and month names rely on exact parsing because
    /// their boundaries cannot be inferred safely.
    /// </summary>
    private static bool TryTokenize(string format, DateTimeFormatInfo info, out List<FormatToken> tokens)
    {
        tokens = new List<FormatToken>();

        var literal = new StringBuilder();
        var fields  = new HashSet<char>();

        for (var index = 0; index < format.Length; index++)
        {
            var character = format[index];
            if (character == '\\')
            {
                if (++index == format.Length)
                {
                    return false;
                }

                literal.Append(format[index]);
                continue;
            }

            if (character == '\'' || character == '"')
            {
                var quote  = character;
                var closed = false;

                while (++index < format.Length)
                {
                    character = format[index];
                    if (character == quote)
                    {
                        closed = true;
                        break;
                    }

                    if (character == '\\' && ++index == format.Length)
                    {
                        return false;
                    }

                    literal.Append(format[index]);
                }

                if (!closed)
                {
                    return false;
                }

                continue;
            }

            if ("yMdHms".IndexOf(character) >= 0)
            {
                var start = index;
                while (index + 1 < format.Length && format[index + 1] == character)
                {
                    index++;
                }

                var length      = index - start + 1;
                var validLength = character == 'y' ? length <= 4 : length <= 2;

                if (!validLength || !fields.Add(character) || (tokens.Count > 0 && literal.Length == 0))
                {
                    return false;
                }

                if (literal.Length > 0)
                {
                    tokens.Add(new FormatToken('\0', literal.ToString()));
                    literal.Clear();
                }

                tokens.Add(new FormatToken(character, string.Empty));
            }
            else if (character == '/' || character == ':')
            {
                var separator = character == '/' ? info.DateSeparator : info.TimeSeparator;
                literal.Append(separator);
            }
            else if ((character >= 'A' && character <= 'Z')
                || (character >= 'a' && character <= 'z') || char.IsDigit(character) || character == '%')
            {
                return false;
            }
            else
            {
                literal.Append(character);
            }
        }

        if (literal.Length > 0)
        {
            tokens.Add(new FormatToken('\0', literal.ToString()));
        }

        return fields.Count > 0;
    }

    /// <summary>
    /// Represents a numeric field or expanded format literal. A null character in Field denotes a
    /// literal.
    /// </summary>
    private readonly struct FormatToken
    {
        internal FormatToken(char field, string literal)
        {
            Field   = field;
            Literal = literal;
        }

        internal char   Field   { get; }
        internal string Literal { get; }
    }
}