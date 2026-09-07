// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Maintains a one-dimensional, critically damped wheel motion in device-independent pixels.
/// The analytic solution is independent of frame rate and remains stable after a delayed frame.
/// </summary>
internal sealed class SmoothScrollMotion(double position)
{
    internal double Position { get; private set; } = position;

    internal double Target { get; private set; } = position;

    internal double Velocity { get; private set; }

    /// <summary>
    /// Accumulates distance without resetting forward velocity. A direction reversal discards queued
    /// travel and velocity so opposite wheel input takes effect immediately instead of fighting inertia.
    /// Returns false when the destination is already at the requested boundary.
    /// </summary>
    internal bool MoveBy(double distance, double maximum)
    {
        if (distance == 0)
        {
            return false;
        }

        var reversing = distance * (Target - Position) < 0 || distance * Velocity < 0;
        var target    = Clamp((reversing ? Position : Target) + distance, maximum);

        if (target == Target && !reversing)
        {
            return false;
        }

        if (reversing)
        {
            Velocity = 0;
        }

        Target = target;
        return true;
    }

    /// <summary>
    /// Clamps continuous position and destination to the available range without importing layout
    /// rounding into the motion. Removed content cannot leave an out-of-range destination behind.
    /// </summary>
    internal void ClampToRange(double maximum)
    {
        Position = Clamp(Position, maximum);
        Target   = Clamp(Target, maximum);

        if ((Target - Position) * Velocity <= 0)
        {
            Velocity = 0;
        }
    }

    /// <summary>
    /// Advances x'' + 2ωx' + ω²(x-target) = 0. The response reaches about 99% of a single
    /// detent in <paramref name="duration"/>; a small position/velocity threshold ends the tail exactly.
    /// </summary>
    internal bool Advance(TimeSpan elapsed, TimeSpan duration)
    {
        var seconds      = Math.Max(0, Math.Min(elapsed.TotalSeconds, duration.TotalSeconds * 10));
        var omega        = 6.64 / duration.TotalSeconds;
        var displacement = Position - Target;
        var coefficient  = Velocity + (omega * displacement);
        var decay        = Math.Exp(-omega * seconds);
        var next         = Target + ((displacement + (coefficient * seconds)) * decay);

        Velocity = (Velocity - (omega * coefficient * seconds)) * decay;

        // Clamping also handles a target pulled closer by a shrinking extent. Even previously high
        // velocity must not carry content past its destination or cause a visible bounce at an edge.
        Position = displacement <= 0 ? Math.Min(next, Target) : Math.Max(next, Target);

        if (Position == Target || (Math.Abs(Position - Target) < 0.1 && Math.Abs(Velocity) < 1))
        {
            Position = Target;
            Velocity = 0;
            return false;
        }

        return true;
    }

    private static double Clamp(double value, double maximum)
    {
        return Math.Max(0, Math.Min(value, maximum));
    }
}
