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
    /// <param name="numberParser">
    /// The parser used for each numeric operand so expression and direct-input culture rules match.
    /// </param>
    /// <param name="result">The computed result, or zero if evaluation failed.</param>
    /// <returns><see langword="true"/> if the expression was evaluated successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryEvaluate(string expression, INumberParser numberParser, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return false;
        }

        if (numberParser is null)
        {
            throw new ArgumentNullException(nameof(numberParser));
        }

        var parser = new Parser(expression, numberParser);
        return parser.TryParse(out result);
    }

    private ref struct Parser
    {
        private readonly ReadOnlySpan<char> _input;
        private readonly INumberParser _numberParser;
        private int _pos;

        public Parser(string input, INumberParser numberParser)
        {
            _input        = input.AsSpan();
            _numberParser = numberParser;
            _pos          = 0;
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

            // Reuse NumberBox's configured parser for every operand. Treating both '.' and ',' as
            // decimal separators corrupts grouping (for example en-US "1,234" became 1.234) and
            // makes expression input disagree with direct numeric input.
            var parsed = _numberParser.ParseDouble(_input[start.._pos].ToString());
            if (parsed is null || double.IsNaN(parsed.Value))
            {
                throw new FormatException($"Invalid number at position {start}.");
            }

            return parsed.Value;
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
