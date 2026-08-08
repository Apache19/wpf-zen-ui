// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides decimal-first arithmetic for <see cref="NumberBox"/> without narrowing its public
/// <see cref="double"/> value domain.
/// </summary>
/// <remarks>
/// <see cref="double"/> is retained by the control API because WPF numeric properties conventionally
/// use it and because its exponent range is substantially wider than <see cref="decimal"/>. User-facing
/// arithmetic nevertheless benefits from decimal semantics: values such as <c>128.45 - 1</c> should
/// remain the decimal number the user entered rather than expose an intermediate IEEE 754 rounding
/// artifact. These helpers use decimal arithmetic only when the operands can round-trip through
/// <see cref="decimal"/> without changing their <see cref="double"/> values; callers can therefore
/// fall back to binary arithmetic for very large, very small, or otherwise unsupported values.
/// </remarks>
internal static class NumberBoxDecimalMath
{
    /// <summary>
    /// Attempts to add two binary values using their lossless decimal representations.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <param name="result">The decimal result converted back to <see cref="double"/>.</param>
    /// <returns>
    /// <see langword="true"/> when both operands and the result fit the decimal domain; otherwise,
    /// <see langword="false"/> so the caller can preserve the wider binary floating-point behavior.
    /// </returns>
    public static bool TryAdd(double left, double right, out double result)
    {
        result = 0D;

        if (!TryConvertExactly(left,  out decimal decimalLeft) ||
            !TryConvertExactly(right, out decimal decimalRight))
        {
            return false;
        }

        try
        {
            result = (double)(decimalLeft + decimalRight);
            return true;
        }
        catch (OverflowException)
        {
            // Decimal has a much smaller exponent range than double. Overflow is not an invalid
            // NumberBox operation, so the caller must retain the established double behavior.
            return false;
        }
    }

    /// <summary>
    /// Attempts to convert a binary value to the decimal number represented by its round-trip text.
    /// </summary>
    /// <remarks>
    /// A direct <see cref="double"/>-to-<see cref="decimal"/> cast rounds to roughly fifteen
    /// significant digits and can silently erase meaningful precision. Parsing the round-trip
    /// representation preserves all significant digits carried by the source value. The final
    /// equality check rejects underflow and any future runtime conversion behavior that would not
    /// reproduce the original binary value.
    /// </remarks>
    private static bool TryConvertExactly(double value, out decimal result)
    {
        result = 0M;

        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return false;
        }

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        return decimal.TryParse(
                   value.ToString("R", CultureInfo.InvariantCulture),
                   NumberStyles.Float,
                   CultureInfo.InvariantCulture,
                   out result
               )
            && (double)result == value;
    }
}