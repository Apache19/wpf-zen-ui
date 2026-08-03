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
    /// <param name="result">The computed result, or <see langword="null"/> if evaluation failed.</param>
    /// <returns><see langword="true"/> if the expression was evaluated successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryEvaluate(string expression, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        var parser = new Parser(expression);
        return parser.TryParse(out result);
    }

    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> _input;
        private int _pos;

        public Parser(string input)
        {
            _input = input.AsSpan();
            _pos   = 0;
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

            while (_pos < _input.Length && (char.IsDigit(_input[_pos]) || _input[_pos] == '.' || _input[_pos] == ','))
            {
                _pos++;
            }

            if (_pos == start)
            {
                throw new FormatException($"Expected number at position {_pos}.");
            }

            var token = _input[start.._pos];

            // Normalize decimal separator to current culture
            Span<char> normalized = stackalloc char[token.Length];
            token.CopyTo(normalized);
            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            for (var i = 0; i < normalized.Length; i++)
            {
                if (normalized[i] == '.' || normalized[i] == ',')
                {
                    normalized[i] = decimalSeparator;
                }
            }

#if NET6_0_OR_GREATER
            if (!double.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out var result))
#else
            if (!double.TryParse(normalized.ToString(), NumberStyles.Number, CultureInfo.CurrentCulture, out var result))
#endif
            {
                throw new FormatException($"Invalid number at position {start}.");
            }

            return result;
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
