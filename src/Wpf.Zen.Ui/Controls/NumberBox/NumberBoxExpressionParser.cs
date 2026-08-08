// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A recursive-descent parser that evaluates basic arithmetic expressions consisting of
/// <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> operators and parentheses.
/// </summary>
/// <remarks>
/// Binary and decimal results are evaluated in parallel. The decimal result is preferred while
/// every operand and operation remains representable by <see cref="decimal"/>; otherwise the parser
/// returns the binary result. This gives normal user-entered numbers intuitive decimal semantics
/// without rejecting expressions that require the wider exponent range of <see cref="double"/>.
/// </remarks>
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
        result = 0D;

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
        private readonly CultureInfo        _culture;
        private          int                _pos;

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
                EvaluationValue value = ParseExpression();
                SkipWhitespace();
                if (_pos != _input.Length)
                {
                    return false;
                }

                result = value.Result;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // expression = term (('+' | '-') term)*
        private EvaluationValue ParseExpression()
        {
            var value = ParseTerm();

            while (true)
            {
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == '+')
                {
                    _pos++;
                    value = EvaluationValue.Add(value, ParseTerm());
                }
                else if (_pos < _input.Length && _input[_pos] == '-')
                {
                    _pos++;
                    value = EvaluationValue.Subtract(value, ParseTerm());
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        // term = unary (('*' | '/') unary)*
        private EvaluationValue ParseTerm()
        {
            var value = ParseUnary();

            while (true)
            {
                SkipWhitespace();
                if (_pos < _input.Length && _input[_pos] == '*')
                {
                    _pos++;
                    value = EvaluationValue.Multiply(value, ParseUnary());
                }
                else if (_pos < _input.Length && _input[_pos] == '/')
                {
                    _pos++;
                    var divisor = ParseUnary();
                    if (divisor.BinaryValue == 0)
                    {
                        throw new DivideByZeroException();
                    }

                    value = EvaluationValue.Divide(value, divisor);
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        // unary = '-' unary | primary
        private EvaluationValue ParseUnary()
        {
            SkipWhitespace();
            if (_pos < _input.Length && _input[_pos] == '-')
            {
                _pos++;
                return EvaluationValue.Negate(ParseUnary());
            }

            if (_pos < _input.Length && _input[_pos] == '+')
            {
                _pos++;
                return ParseUnary();
            }

            return ParsePrimary();
        }

        // primary = number | '(' expression ')'
        private EvaluationValue ParsePrimary()
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

        private EvaluationValue ParseNumber()
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

            string token = _input[start.._pos].ToString();
            if (!double.TryParse(
                    token,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    _culture,
                    out double parsed
                )
             || double.IsNaN(parsed)
             || double.IsInfinity(parsed))
            {
                throw new FormatException($"Invalid number at position {start}.");
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            bool hasDecimalValue = decimal.TryParse(
                                       token,
                                       NumberStyles.Float | NumberStyles.AllowThousands,
                                       _culture,
                                       out decimal decimalValue
                                   )
                                && (double)decimalValue == parsed;

            return new EvaluationValue(parsed, decimalValue, hasDecimalValue);
        }

        private void SkipWhitespace()
        {
            while (_pos < _input.Length && char.IsWhiteSpace(_input[_pos]))
            {
                _pos++;
            }
        }
    }

    /// <summary>
    /// Carries the established binary result alongside an optional decimal result so an overflow
    /// late in an expression can fall back to the result of evaluating the entire expression with
    /// <see cref="double"/>, rather than mixing decimal intermediates into the fallback path.
    /// </summary>
    private readonly struct EvaluationValue
    {
        public EvaluationValue(double binaryValue, decimal decimalValue, bool hasDecimalValue)
        {
            BinaryValue     = binaryValue;
            DecimalValue    = decimalValue;
            HasDecimalValue = hasDecimalValue;
        }

        public double BinaryValue { get; }

        private decimal DecimalValue { get; }

        private bool HasDecimalValue { get; }

        public double Result => HasDecimalValue ? (double)DecimalValue : BinaryValue;

        public static EvaluationValue Add(EvaluationValue left, EvaluationValue right) =>
            Create(
                left.BinaryValue + right.BinaryValue,
                left,
                right,
                static (x, y) => x + y
            );

        public static EvaluationValue Subtract(EvaluationValue left, EvaluationValue right) =>
            Create(
                left.BinaryValue - right.BinaryValue,
                left,
                right,
                static (x, y) => x - y
            );

        public static EvaluationValue Multiply(EvaluationValue left, EvaluationValue right) =>
            Create(
                left.BinaryValue * right.BinaryValue,
                left,
                right,
                static (x, y) => x * y
            );

        public static EvaluationValue Divide(EvaluationValue left, EvaluationValue right) =>
            Create(
                left.BinaryValue / right.BinaryValue,
                left,
                right,
                static (x, y) => x / y
            );

        public static EvaluationValue Negate(EvaluationValue value)
        {
            if (!value.HasDecimalValue)
            {
                return new EvaluationValue(-value.BinaryValue, 0M, false);
            }

            try
            {
                return new EvaluationValue(-value.BinaryValue, -value.DecimalValue, true);
            }
            catch (OverflowException)
            {
                return new EvaluationValue(-value.BinaryValue, 0M, false);
            }
        }

        private static EvaluationValue Create(
            double binaryValue,
            EvaluationValue left,
            EvaluationValue right,
            Func<decimal, decimal, decimal> operation
        )
        {
            if (!left.HasDecimalValue || !right.HasDecimalValue)
            {
                return new EvaluationValue(binaryValue, 0M, false);
            }

            try
            {
                return new EvaluationValue(
                    binaryValue,
                    operation(left.DecimalValue, right.DecimalValue),
                    true
                );
            }
            catch (OverflowException)
            {
                // Losing decimal support is local to the numeric representation, not a parse
                // failure. The binary value has been evaluated independently and remains valid.
                return new EvaluationValue(binaryValue, 0M, false);
            }
        }
    }
}