// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Text;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// 按显式格式、标准格式和字段范围识别日期时间片段，并补齐缺失字段。
/// </summary>
internal static class DateTimePickerTextParser
{
    /// <summary>
    /// 先精确解析；失败时结合分隔符和数值范围识别片段，无法确定字段时保留输入。
    /// 缺少年份采用当前年份，纯时间采用当天日期，其余字段使用最小初始值。
    /// </summary>
    internal static bool TryParse(string text, string format, CultureInfo culture, out DateTime value)
    {
        bool exact = DateTime.TryParseExact(text, format, culture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out value);
        if (exact)
        {
            return true;
        }

        string input = text.Trim();
        var now = DateTime.Now;
        // 孤立数值必须排除月、日、时、分、秒的所有可能性；不使用两位年份窗口。
        if (int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
        {
            if (input.Length > 4 || number <= 59)
            {
                return false;
            }
            var yearFields = new Dictionary<char, int> { ['y'] = number };
            return TryCreate(yearFields, culture, now, out value);
        }

        // 显式格式优先决定字段顺序；其次采用 ISO 和当前区域的标准日期时间模式。
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
        foreach (string pattern in formats)
        {
            if (!TryTokenize(pattern, culture.DateTimeFormat, out var tokens))
            {
                continue;
            }
            if (TryPattern(input, tokens, culture, now, out value))
            {
                return true;
            }

            // 去掉年份及其相邻分隔符，保留格式约定的月日顺序及日期后的时间部分。
            int yearIndex = tokens.FindIndex(token => token.Field == 'y');
            if (yearIndex >= 0)
            {
                var withoutYear = new List<FormatToken>(tokens);
                int separatorIndex = yearIndex == 0 ? yearIndex + 1 : yearIndex - 1;
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

            int hourIndex = tokens.FindIndex(token => token.Field == 'H');
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
    /// 匹配有分隔符的字段前缀，至少需要两个字段或带单位的字段，避免猜测孤立数值。
    /// </summary>
    private static bool TryPattern(string input, List<FormatToken> tokens, CultureInfo culture,
        DateTime now, out DateTime value)
    {
        value = default;
        int position = 0;
        bool hasLiteral = false;
        var fields = new Dictionary<char, int>();
        for (int index = 0; index < tokens.Count && position < input.Length; index++)
        {
            var token = tokens[index];
            if (token.Field == '\0')
            {
                string literal = token.Literal;
                string finalLiteral = literal.TrimEnd();
                string remainder = input.Substring(position);
                if (string.Equals(remainder, finalLiteral, StringComparison.Ordinal))
                {
                    literal = finalLiteral;
                }
                if (input.Length - position < literal.Length
                    || string.CompareOrdinal(input, position, literal, 0, literal.Length) != 0)
                {
                    return false;
                }
                position += literal.Length;
                hasLiteral = true;
                if (position == input.Length && index + 1 < tokens.Count
                    && (literal.Length == 0 || !char.IsLetter(literal[literal.Length - 1])))
                {
                    return false;
                }
                continue;
            }
            int start = position;
            while (position < input.Length && input[position] >= '0' && input[position] <= '9')
            {
                position++;
            }
            int length = position - start;
            int maximumLength = token.Field == 'y' ? 4 : 2;
            if (length == 0 || length > maximumLength)
            {
                return false;
            }
            string digits = input.Substring(start, length);
            int number = int.Parse(digits, CultureInfo.InvariantCulture);
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
    /// 月日输入补当前年，纯时间补当天日期，其余未输入字段使用最小初始值。
    /// 使用区域日历构造结果，以统一验证闰日、范围与年月日组合。
    /// </summary>
    private static bool TryCreate(Dictionary<char, int> fields, CultureInfo culture,
        DateTime now, out DateTime value)
    {
        value = default;
        var calendar = culture.DateTimeFormat.Calendar;
        bool timeOnly = !fields.ContainsKey('y') && !fields.ContainsKey('M') && !fields.ContainsKey('d');
        int year = GetField(fields, 'y', calendar.GetYear(now));
        int defaultMonth = timeOnly ? calendar.GetMonth(now) : 1;
        int defaultDay = timeOnly ? calendar.GetDayOfMonth(now) : 1;
        int month = GetField(fields, 'M', defaultMonth);
        int day = GetField(fields, 'd', defaultDay);
        int hour = GetField(fields, 'H', 0);
        int minute = GetField(fields, 'm', 0);
        int second = GetField(fields, 's', 0);
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
    /// 读取已识别字段或该字段的补全初始值。
    /// </summary>
    private static int GetField(Dictionary<char, int> fields, char field, int defaultValue)
    {
        return fields.TryGetValue(field, out int value) ? value : defaultValue;
    }

    /// <summary>
    /// 识别数字字段及 .NET 格式中的字面量、转义和区域分隔符。
    /// 连续无分隔数字、重复字段及月份名称等格式只走标准精确解析，避免猜测字段边界。
    /// </summary>
    private static bool TryTokenize(string format, DateTimeFormatInfo info, out List<FormatToken> tokens)
    {
        tokens = new List<FormatToken>();
        var literal = new StringBuilder();
        var fields = new HashSet<char>();
        for (int index = 0; index < format.Length; index++)
        {
            char character = format[index];
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
                char quote = character;
                bool closed = false;
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
                int start = index;
                while (index + 1 < format.Length && format[index + 1] == character)
                {
                    index++;
                }
                int length = index - start + 1;
                bool validLength = character == 'y' ? length <= 4 : length <= 2;
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
                string separator = character == '/' ? info.DateSeparator : info.TimeSeparator;
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
    /// 一个数字字段或已展开的格式字面量；Field 为零字符时表示字面量。
    /// </summary>
    private readonly struct FormatToken
    {
        internal FormatToken(char field, string literal)
        {
            Field = field;
            Literal = literal;
        }

        internal char Field { get; }
        internal string Literal { get; }
    }
}
