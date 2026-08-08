// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A simple recursive-descent parser that evaluates basic arithmetic expressions
/// consisting of <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> operators and parentheses.
/// </summary>
internal static class NumberBoxExpressionParser
{
    /// <summary>
    /// Attempts to evaluate <paramref name="expression"/> as a basic arithmetic expression.
    /// </summary>
    /// <param name="expression">The expression string to evaluate.</param>
    /// <param name="culture">The culture used to parse each numeric operand.</param>
    /// <param name="result">The computed result, or zero if evaluation failed.</param>
    /// <returns><see langword="true"/> if the expression was evaluated successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryEvaluate(string expression, CultureInfo culture, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        if (culture is null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var parser = new Parser(expression, culture);
        return parser.TryParse(out result);
    }

    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> _input;
        private readonly CultureInfo _culture;
        private int _pos;

        public Parser(string input, CultureInfo culture)
        {
            _input   = input.AsSpan();
            _culture = culture;
            _pos     = 0;
        }

        public bool TryParse(out double result)
        {
            result = 0;
            try
            {
                result = ParseExpression();
                SkipWhitespace();
                return _pos == _input.Length;
            }
            catch
            {
                return false;
            }
        }

        // expression = term (('+' | '-') term)*
        private double ParseExpression()
        {
            var value = ParseTerm();

            while (true)
            {
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == '+')
                {
                    _pos++;
                    value += ParseTerm();
                }
                else if (_pos < _input.Length && _input[_pos] == '-')
                {
                    _pos++;
                    value -= ParseTerm();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        // term = unary (('*' | '/') unary)*
        private double ParseTerm()
        {
            var value = ParseUnary();

            while (true)
            {
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == '*')
                {
                    _pos++;
                    value *= ParseUnary();
                }
                else if (_pos < _input.Length && _input[_pos] == '/')
                {
                    _pos++;
                    var divisor = ParseUnary();
                    if (divisor == 0)
                    {
                        throw new DivideByZeroException();
                    }

                    value /= divisor;
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        // unary = '-' unary | primary
        private double ParseUnary()
        {
            SkipWhitespace();
            if (_pos < _input.Length && _input[_pos] == '-')
            {
                _pos++;
                return -ParseUnary();
            }

            if (_pos < _input.Length && _input[_pos] == '+')
            {
                _pos++;
                return ParseUnary();
            }

            return ParsePrimary();
        }

        // primary = number | '(' expression ')'
        private double ParsePrimary()
        {
            SkipWhitespace();

            if (_pos >= _input.Length)
            {
                throw new FormatException("Unexpected end of expression.");
            }

            if (_input[_pos] == '(')
            {
                _pos++;
                var value = ParseExpression();
                SkipWhitespace();
                if (_pos >= _input.Length || _input[_pos] != ')')
                {
                    throw new FormatException("Missing closing parenthesis.");
                }

                _pos++;
                return value;
            }

            return ParseNumber();
        }

        private double ParseNumber()
        {
            SkipWhitespace();
            var start = _pos;

            NumberFormatInfo format = _culture.NumberFormat;
            while (_pos < _input.Length)
            {
                char character = _input[_pos];
                if (char.IsDigit(character)
                    || format.NumberDecimalSeparator.Contains(character)
                    || format.NumberGroupSeparator.Contains(character)
                    || character is 'e' or 'E')
                {
                    _pos++;
                    continue;
                }

                if (character is '+' or '-'
                    && _pos > start
                    && _input[_pos - 1] is 'e' or 'E')
                {
                    _pos++;
                    continue;
                }

                break;
            }

            if (_pos == start)
            {
                throw new FormatException($"Expected number at position {_pos}.");
            }

            if (!double.TryParse(
                    _input[start.._pos].ToString(),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    _culture,
                    out double parsed
                )
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                throw new FormatException($"Invalid number at position {start}.");
            }

            return parsed;
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
            {
                _pos++;
            }
        }
    }
}
