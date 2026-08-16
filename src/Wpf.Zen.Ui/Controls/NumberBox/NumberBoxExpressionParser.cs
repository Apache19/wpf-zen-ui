// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// An iterative parser that evaluates basic arithmetic expressions consisting of
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
                var values         = new List<EvaluationValue>();
                var operators      = new List<Operator>();
                var expectsOperand = true;

                while (true)
                {
                    SkipWhitespace();

                    if (_pos >= _input.Length)
                    {
                        break;
                    }

                    if (expectsOperand)
                    {
                        expectsOperand = !ParseOperand(values, operators);
                        continue;
                    }

                    var character = _input[_pos];
                    if (character == ')')
                    {
                        _pos++;
                        ReduceParenthesizedExpression(values, operators);
                        continue;
                    }

                    if (!TryGetBinaryOperator(character, out var currentOperator))
                    {
                        return false;
                    }

                    _pos++;

                    while (operators.Count > 0
                        && GetPrecedence(operators[^1]) >= GetPrecedence(currentOperator))
                    {
                        ApplyTopOperator(values, operators);
                    }

                    operators.Add(currentOperator);
                    expectsOperand = true;
                }

                if (expectsOperand || values.Count == 0)
                {
                    return false;
                }

                while (operators.Count > 0)
                {
                    if (operators[^1] == Operator.LeftParenthesis)
                    {
                        return false;
                    }

                    ApplyTopOperator(values, operators);
                }

                if (values.Count != 1)
                {
                    return false;
                }

                result = values[0].Result;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses the operand-side grammar without recursion. Unary signs are folded before the
        /// operand is pushed; a negation that precedes a parenthesis is retained on the operator
        /// stack and applied when that parenthesized expression closes.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> when a numeric operand was completed; <see langword="false"/>
        /// when an opening parenthesis requires another operand on the next iteration.
        /// </returns>
        private bool ParseOperand(List<EvaluationValue> values, List<Operator> operators)
        {
            var negate = false;

            while (true)
            {
                SkipWhitespace();

                if (_pos >= _input.Length)
                {
                    throw new FormatException("Unexpected end of expression.");
                }

                if (_input[_pos] == '+')
                {
                    _pos++;
                    continue;
                }

                if (_input[_pos] == '-')
                {
                    _pos++;
                    negate = !negate;
                    continue;
                }

                break;
            }

            if (_input[_pos] == '(')
            {
                if (negate)
                {
                    operators.Add(Operator.Negate);
                }

                _pos++;
                operators.Add(Operator.LeftParenthesis);
                return false;
            }

            var value = ParseNumber();
            values.Add(negate ? EvaluationValue.Negate(value) : value);
            return true;
        }

        private static void ReduceParenthesizedExpression(
            List<EvaluationValue> values,
            List<Operator> operators
        )
        {
            while (operators.Count > 0 && operators[^1] != Operator.LeftParenthesis)
            {
                ApplyTopOperator(values, operators);
            }

            if (operators.Count == 0)
            {
                throw new FormatException("Unexpected closing parenthesis.");
            }

            operators.RemoveAt(operators.Count - 1);
            if (operators.Count > 0 && operators[^1] == Operator.Negate)
            {
                ApplyTopOperator(values, operators);
            }
        }

        private static void ApplyTopOperator(
            List<EvaluationValue> values,
            List<Operator> operators
        )
        {
            var operation = operators[^1];
            operators.RemoveAt(operators.Count - 1);

            if (operation == Operator.Negate)
            {
                if (values.Count == 0)
                {
                    throw new FormatException("Missing operand for unary negation.");
                }

                values[^1] = EvaluationValue.Negate(values[^1]);
                return;
            }

            if (values.Count < 2)
            {
                throw new FormatException("Missing operand for binary operator.");
            }

            var right = values[^1];
            values.RemoveAt(values.Count - 1);
            var left  = values[^1];

            values[^1] = operation switch
            {
                Operator.Add      => EvaluationValue.Add(left, right),
                Operator.Subtract => EvaluationValue.Subtract(left, right),
                Operator.Multiply => EvaluationValue.Multiply(left, right),
                Operator.Divide when right.BinaryValue != 0 => EvaluationValue.Divide(left, right),
                Operator.Divide   => throw new DivideByZeroException(),
                _ => throw new FormatException("Invalid operator state."),
            };
        }

        private static bool TryGetBinaryOperator(char character, out Operator operation)
        {
            operation = character switch
            {
                '+' => Operator.Add,
                '-' => Operator.Subtract,
                '*' => Operator.Multiply,
                '/' => Operator.Divide,
                _   => Operator.None,
            };
            return operation != Operator.None;
        }

        private static int GetPrecedence(Operator operation) => operation switch
        {
            Operator.Add or Operator.Subtract    => 1,
            Operator.Multiply or Operator.Divide => 2,
            Operator.Negate                      => 3,
            _                                    => -1,
        };

        private EvaluationValue ParseNumber()
        {
            SkipWhitespace();

            var start  = _pos;
            var format = _culture.NumberFormat;

            while (_pos < _input.Length)
            {
                var character = _input[_pos];

                if (char.IsDigit(character)
                    || format.NumberDecimalSeparator.Contains(character)
                    || format.NumberGroupSeparator.Contains(character)
                    || character is 'e' or 'E')
                {
                    _pos++;
                    continue;
                }

                if (character is '+' or '-' && _pos > start && _input[_pos - 1] is 'e' or 'E')
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

            var token = _input[start.._pos].ToString();

            if (!double.TryParse(
                    token,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    _culture,
                    out var parsed
                )
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed))
            {
                throw new FormatException($"Invalid number at position {start}.");
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var hasDecimalValue =
                decimal.TryParse(
                    token,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    _culture,
                    out var decimalValue
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

    private enum Operator
    {
        None,
        Add,
        Subtract,
        Multiply,
        Divide,
        Negate,
        LeftParenthesis,
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