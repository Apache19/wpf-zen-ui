// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides a scroll viewer that dynamically tracks vertical and horizontal scrolling activity,
/// enabling automatic visibility and hiding of scrollbars based on user interaction.
/// </summary>
[DefaultEvent("ScrollChangedEvent")]
public sealed class DynamicScrollViewer : PassiveScrollViewer, IDisposable
{
    private CancellationTokenSource? _verScrollCts;

    private CancellationTokenSource? _horScrollCts;

    // Due to the large number of triggered events, we limit the complex logic of DependencyProperty
    private bool _scrollingVertically
        = (bool)IsScrollingVerticallyProperty.DefaultMetadata.DefaultValue;

    private bool _scrollingHorizontally
        = (bool)IsScrollingHorizontallyProperty.DefaultMetadata.DefaultValue;

    private double _minimalChange
        = (double)MinimalChangeProperty.DefaultMetadata.DefaultValue;

    private int _timeout
        = (int)TimeoutProperty.DefaultMetadata.DefaultValue;

    /// <summary>Identifies the <see cref="IsScrollingVertically"/> dependency property.</summary>
    public static readonly DependencyProperty IsScrollingVerticallyProperty = DependencyProperty.Register(
        nameof(IsScrollingVertically),
        typeof(bool),
        typeof(DynamicScrollViewer),
        new PropertyMetadata(
            false,
            static (d, e) => ((DynamicScrollViewer)d)._scrollingVertically = (bool)e.NewValue
        )
    );

    /// <summary>Identifies the <see cref="IsScrollingHorizontally"/> dependency property.</summary>
    public static readonly DependencyProperty IsScrollingHorizontallyProperty = DependencyProperty.Register(
        nameof(IsScrollingHorizontally),
        typeof(bool),
        typeof(DynamicScrollViewer),
        new PropertyMetadata(
            false,
            static (d, e) => ((DynamicScrollViewer)d)._scrollingHorizontally = (bool)e.NewValue
        )
    );

    /// <summary>Identifies the <see cref="MinimalChange"/> dependency property.</summary>
    public static readonly DependencyProperty MinimalChangeProperty = DependencyProperty.Register(
        nameof(MinimalChange),
        typeof(double),
        typeof(DynamicScrollViewer),
        new PropertyMetadata(
            40d,
            static (d, e) => ((DynamicScrollViewer)d)._minimalChange = (double)e.NewValue
        )
    );

    /// <summary>Identifies the <see cref="Timeout"/> dependency property.</summary>
    public static readonly DependencyProperty TimeoutProperty = DependencyProperty.Register(
        nameof(Timeout),
        typeof(int),
        typeof(DynamicScrollViewer),
        new PropertyMetadata(
            1200,
            static (d, e) => ((DynamicScrollViewer)d)._timeout = (int)e.NewValue
        )
    );

    static DynamicScrollViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DynamicScrollViewer),
            new FrameworkPropertyMetadata(typeof(DynamicScrollViewer))
        );
    }

    public DynamicScrollViewer()
    {
        Unloaded += (_, _) => Dispose();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user was scrolling vertically for the last few seconds.
    /// </summary>
    public bool IsScrollingVertically
    {
        get => (bool)GetValue(IsScrollingVerticallyProperty);
        set => SetValue(IsScrollingVerticallyProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user was scrolling horizontally for the last few seconds.
    /// </summary>
    public bool IsScrollingHorizontally
    {
        get => (bool)GetValue(IsScrollingHorizontallyProperty);
        set => SetValue(IsScrollingHorizontallyProperty, value);
    }

    /// <summary>
    /// Gets or sets the value required for the scroll to show automatically.
    /// </summary>
    public double MinimalChange
    {
        get => (double)GetValue(MinimalChangeProperty);
        set => SetValue(MinimalChangeProperty, value);
    }

    /// <summary>
    /// Gets or sets time after which the scroll is to be hidden.
    /// </summary>
    public int Timeout
    {
        get => (int)GetValue(TimeoutProperty);
        set => SetValue(TimeoutProperty, value);
    }

    /// <summary>
    /// OnScrollChanged is an override called whenever scrolling state changes on this <see cref="DynamicScrollViewer"/>.
    /// </summary>
    /// <remarks>
    /// OnScrollChanged fires the ScrollChangedEvent. Overriders of this method should call
    /// base.OnScrollChanged(args) if they want the event to be fired.
    /// </remarks>
    /// <param name="e">ScrollChangedEventArgs containing information about the change in scrolling state.</param>
    protected override void OnScrollChanged(ScrollChangedEventArgs e)
    {
        base.OnScrollChanged(e);

        if (e.HorizontalChange > _minimalChange || e.HorizontalChange < -_minimalChange)
        {
            _horScrollCts?.Cancel();
            _horScrollCts?.Dispose();

            _horScrollCts = new CancellationTokenSource();

            _ = UpdateHorizontalScrollingStateAsync(_horScrollCts.Token);
        }

        if (e.VerticalChange > _minimalChange || e.VerticalChange < -_minimalChange)
        {
            _verScrollCts?.Cancel();
            _verScrollCts?.Dispose();

            _verScrollCts = new CancellationTokenSource();

            _ = UpdateVerticalScrollingStateAsync(_verScrollCts.Token);
        }
    }

    private async Task UpdateVerticalScrollingStateAsync(CancellationToken ct)
    {
        if (!_scrollingVertically)
        {
            SetCurrentValue(IsScrollingVerticallyProperty, true);
        }

        try
        {
            await Task.Delay(_timeout is < 10000 and > 1000 ? _timeout : 1000, ct);

            if (!ct.IsCancellationRequested && _scrollingVertically)
            {
                SetCurrentValue(IsScrollingVerticallyProperty, false);
            }
        }
        catch (TaskCanceledException)
        {
            // Normal cancellation, no handling required.
        }
    }

    private async Task UpdateHorizontalScrollingStateAsync(CancellationToken ct)
    {
        if (!_scrollingHorizontally)
        {
            SetCurrentValue(IsScrollingHorizontallyProperty, true);
        }

        try
        {
            await Task.Delay(_timeout is < 10000 and > 1000 ? _timeout : 1000, ct);

            if (!ct.IsCancellationRequested && _scrollingHorizontally)
            {
                SetCurrentValue(IsScrollingHorizontallyProperty, false);
            }
        }
        catch (TaskCanceledException)
        {
            // Normal cancellation, no handling required.
        }
    }

    public void Dispose()
    {
        if (_horScrollCts != null)
        {
            _horScrollCts.Cancel();
            _horScrollCts.Dispose();
        }

        if (_verScrollCts != null)
        {
            _verScrollCts.Dispose();
            _verScrollCts.Dispose();
        }
    }
}