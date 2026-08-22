// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides the read-only presentation data for one item in a
/// <see cref="RatingControl.ItemTemplate"/>.
/// </summary>
/// <remarks>
/// <para>
/// The control owns and updates these objects. Public setters are intentionally omitted so a
/// template cannot put input hit testing and rendered state out of sync.
/// </para>
/// <para>
/// Bind to <see cref="State"/> and <see cref="DisplayMode"/> for discrete XAML triggers, or to
/// <see cref="Fill"/> when a custom visualization needs the normalized numeric fraction. The item
/// deliberately exposes no icon, text, or other presentation-system value: an item template owns
/// that mapping and may render stars, shapes, images, text, or any application-specific content.
/// </para>
/// </remarks>
public sealed class RatingItem : INotifyPropertyChanged
{
    private double                _fill;
    private RatingItemState       _state;
    private RatingItemDisplayMode _displayMode;

    internal RatingItem(int index)
    {
        Index = index;
        Position = index + 1;
    }

    /// <summary>
    /// Gets the zero-based position of this item in the rating sequence.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the one-based rating position represented by the complete item.
    /// </summary>
    /// <remarks>
    /// For example, the third item has a position of <c>3</c>. This is useful for labels,
    /// automation content, or position-specific template styling.
    /// </remarks>
    public int Position { get; }

    /// <summary>
    /// Gets the normalized filled fraction of this item in the inclusive range from zero to one.
    /// </summary>
    public double Fill
    {
        get => _fill;
        private set => SetField(ref _fill, value, nameof(Fill));
    }

    /// <summary>
    /// Gets the discrete star state after display rounding has been applied.
    /// </summary>
    public RatingItemState State
    {
        get => _state;
        private set => SetField(ref _state, value, nameof(State));
    }

    /// <summary>
    /// Gets the source of the score currently represented by this item.
    /// </summary>
    public RatingItemDisplayMode DisplayMode
    {
        get => _displayMode;
        private set => SetField(ref _displayMode, value, nameof(DisplayMode));
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Refreshes every template-visible facet of this item from one canonical fill value.
    /// </summary>
    /// <remarks>
    /// The fill arriving here has already been rounded by the control. Converting it to integer
    /// quarters avoids fragile floating-point equality checks and keeps the numeric and discrete
    /// template states on the same canonical value. Visual selection remains exclusively a
    /// template concern.
    /// </remarks>
    internal void Update(double fill, RatingItemDisplayMode displayMode)
    {
        int quarters = Math.Max(0, Math.Min(4, (int)Math.Round(fill * 4, MidpointRounding.AwayFromZero)));

        Fill = quarters / 4.0;
        State = quarters switch
        {
            1 => RatingItemState.QuarterStar,
            2 => RatingItemState.HalfStar,
            3 => RatingItemState.ThreeQuarterStar,
            4 => RatingItemState.FullStar,
            _ => RatingItemState.Empty,
        };
        DisplayMode = displayMode;
    }

    private void SetField<T>(ref T field, T value, string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
