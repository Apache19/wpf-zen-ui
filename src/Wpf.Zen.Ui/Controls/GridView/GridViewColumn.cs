// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a GridView column whose width can be Auto, pixel, or star sized.
/// </summary>
/// <remarks>
/// <para>
/// WPF exposes column width as a <see cref="double"/> where <see cref="double.NaN"/> means Auto.
/// This derived column presents the same concept as a <see cref="GridLength"/> so XAML can use
/// <c>Auto</c>, fixed values, <c>*</c>, and weighted star values consistently with a WPF
/// <see cref="System.Windows.Controls.Grid"/>.
/// </para>
/// <para>
/// Auto measurement is still performed by WPF's native GridView presenters. Star allocation is
/// coordinated once per owning ScrollViewer by the library's Fluent GridView template. Dragging a
/// star column preserves its star definition and changes its weight while columns to its right
/// absorb the difference. Dragging an Auto or fixed column converts that column to pixels, and
/// double-clicking an interior gripper restores Auto.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:GridView&gt;
///     &lt;ui:GridViewColumn Width="Auto" Header="Name" DisplayMemberBinding="{Binding Name}" /&gt;
///     &lt;ui:GridViewColumn Width="2*" Header="Description" DisplayMemberBinding="{Binding Description}" /&gt;
///     &lt;ui:GridViewColumn Width="120" Header="Status" DisplayMemberBinding="{Binding Status}" /&gt;
/// &lt;/ui:GridView&gt;
/// </code>
/// </example>
public class GridViewColumn : NativeGridViewColumn
{
    // WPF deliberately keeps the shared Auto desired width internal. MinWidth and MaxWidth cannot
    // be applied after native header/cell measurement through any public API, so these two cached
    // members are the narrow reflection boundary for the GridLength enhancement. Failure leaves
    // ordinary native Auto sizing intact and permanently disables further reflective work.
    private static readonly Lazy<FieldInfo?> _desiredWidthField = new(static () =>
        typeof(NativeGridViewColumn).GetField("_desiredWidth", BindingFlags.NonPublic | BindingFlags.Instance)
    );

    private static readonly Lazy<MethodInfo?> _updateActualWidthMethod = new(static () =>
        typeof(NativeGridViewColumn).GetMethod(
            "UpdateActualWidth",
            BindingFlags.NonPublic | BindingFlags.Instance
        )
    );

    private static bool _isNativeAutoWidthAccessAvailable = true;
    private bool _isSynchronizingNativeWidth;
    private bool _isSynchronizingStarDefinition;

    static GridViewColumn()
    {
        // The native presenter and header gripper continue to read and write the base double DP.
        // Observing that property is what preserves native drag and double-click behavior while the
        // public property below exposes a GridLength definition.
#pragma warning disable WPF0005 // Both the native and GridLength dependency properties are named Width.
        NativeGridViewColumn.WidthProperty.OverrideMetadata(
            typeof(GridViewColumn),
            new PropertyMetadata(double.NaN, OnNativeWidthChanged)
        );
#pragma warning restore WPF0005
    }

    /// <summary>
    /// Identifies the <see cref="Width"/> dependency property.
    /// </summary>
#pragma warning disable WPF0005 // The callback name distinguishes the definition from the hidden native Width DP.
    public static new readonly DependencyProperty WidthProperty = DependencyProperty.Register(
        nameof(Width),
        typeof(GridLength),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(GridLength.Auto, OnWidthDefinitionChanged)
    );
#pragma warning restore WPF0005

    /// <summary>
    /// Identifies the <see cref="MinWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MinWidthProperty = DependencyProperty.Register(
        nameof(MinWidth),
        typeof(double),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(0.0, OnMinWidthChanged),
        ValidateMinWidth
    );

    /// <summary>
    /// Identifies the <see cref="MaxWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxWidthProperty = DependencyProperty.Register(
        nameof(MaxWidth),
        typeof(double),
        typeof(GridViewColumn),
        new FrameworkPropertyMetadata(double.PositiveInfinity, OnMaxWidthChanged, CoerceMaxWidth),
        ValidateMaxWidth
    );

    /// <summary>
    /// Gets or sets the column width definition. The default is <see cref="GridLength.Auto"/>.
    /// </summary>
    /// <remarks>
    /// Auto uses the widest header or realized cell measured by WPF, an absolute value uses device-
    /// independent pixels, and star values divide the remaining viewport width by weight.
    /// </remarks>
    public new GridLength Width
    {
        get => (GridLength)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum rendered width of the column. The default is <c>0</c>.
    /// </summary>
    /// <remarks>
    /// Changing this value invalidates Auto measurement and redistributes star columns. It does not
    /// affect row height or any other layout dimension.
    /// </remarks>
    public double MinWidth
    {
        get => (double)GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum rendered width of the column. The default is positive infinity.
    /// </summary>
    /// <remarks>
    /// Changing this value invalidates Auto measurement and redistributes star columns. A value
    /// smaller than <see cref="MinWidth"/> is coerced to <see cref="MinWidth"/>.
    /// </remarks>
    public double MaxWidth
    {
        get => (double)GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    /// <summary>
    /// Raised internally when a width definition or constraint requires star redistribution.
    /// </summary>
    internal event EventHandler? LayoutPropertyChanged;

    /// <summary>
    /// Raised when WPF's native header gripper requests a finite width for a star column.
    /// </summary>
    internal event EventHandler<GridViewColumnResizeRequestedEventArgs>? ResizeRequested;

    /// <summary>
    /// Gets the double width consumed by WPF's native GridView presenters.
    /// </summary>
    internal double NativeWidth => (double)GetValue(NativeGridViewColumn.WidthProperty);

    /// <summary>
    /// Restricts a proposed width to this column's declared bounds.
    /// </summary>
    internal double CoerceWidth(double value)
    {
        return Math.Max(MinWidth, Math.Min(value, MaxWidth));
    }

    /// <summary>
    /// Publishes a resolved star width to WPF without replacing the star definition.
    /// </summary>
    internal bool SetStarActualWidth(double value)
    {
        double coercedWidth = CoerceWidth(value);
        if (AreClose(NativeWidth, coercedWidth))
        {
            return false;
        }

        SetNativeWidth(coercedWidth);
        return true;
    }

    /// <summary>
    /// Replaces a star weight without publishing the temporary placeholder used for an ordinary
    /// application-authored width-definition change.
    /// </summary>
    internal void SetStarWeight(double value)
    {
        if (!Width.IsStar || AreClose(Width.Value, value))
        {
            return;
        }

        _isSynchronizingStarDefinition = true;
        try
        {
            SetCurrentValue(WidthProperty, new GridLength(value, GridUnitType.Star));
        }
        finally
        {
            _isSynchronizingStarDefinition = false;
        }
    }

    /// <summary>
    /// Publishes the pixel result of an interactive boundary resize for an Auto or fixed column.
    /// </summary>
    /// <remarks>
    /// Native GridView has no separate desired and display widths. Consequently, a non-star column
    /// that absorbs a neighboring star resize becomes fixed, exactly as it does when its own native
    /// gripper is dragged. Keeping this transition here prevents the layout algorithm from writing
    /// the hidden native <see cref="NativeGridViewColumn.WidthProperty"/> directly.
    /// </remarks>
    internal void SetInteractiveWidth(double value)
    {
        SetCurrentValue(WidthProperty, new GridLength(CoerceWidth(value)));
    }

    /// <summary>
    /// Applies MinWidth and MaxWidth to WPF's shared native Auto measurement.
    /// </summary>
    internal void CoerceAutoDesiredWidth()
    {
        if (!_isNativeAutoWidthAccessAvailable || !Width.IsAuto)
        {
            return;
        }

        FieldInfo? desiredWidthField = _desiredWidthField.Value;
        MethodInfo? updateActualWidthMethod = _updateActualWidthMethod.Value;
        if (desiredWidthField is null || updateActualWidthMethod is null)
        {
            _isNativeAutoWidthAccessAvailable = false;
            return;
        }

        try
        {
            if (desiredWidthField.GetValue(this) is not double desiredWidth)
            {
                _isNativeAutoWidthAccessAvailable = false;
                return;
            }

            double coercedWidth = CoerceWidth(desiredWidth);
            if (AreClose(desiredWidth, coercedWidth))
            {
                return;
            }

            desiredWidthField.SetValue(this, coercedWidth);
            _ = updateActualWidthMethod.Invoke(this, null);
        }
        catch (Exception)
        {
            // Reflection only augments Auto constraints. A runtime with changed internals retains
            // fully functional native Auto sizing, and the hot layout path does not repeatedly throw.
            _isNativeAutoWidthAccessAvailable = false;
        }
    }

    private static void OnWidthDefinitionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        if (column._isSynchronizingStarDefinition)
        {
            return;
        }

        column.ApplyWidthDefinition(resetAutoMeasurement: false);
        column.LayoutPropertyChanged?.Invoke(column, EventArgs.Empty);
    }

    private static void OnNativeWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        if (column._isSynchronizingNativeWidth)
        {
            return;
        }

#pragma warning disable WPF0020, WPF0022 // The derived Width DP intentionally replaces the native double DP with GridLength.
        double nativeWidth = (double)e.NewValue;
        if (!double.IsNaN(nativeWidth) && column.Width.IsStar)
        {
            var resizeRequest = new GridViewColumnResizeRequestedEventArgs(
                (double)e.OldValue,
                column.CoerceWidth(nativeWidth)
            );
            column.ResizeRequested?.Invoke(column, resizeRequest);
            if (resizeRequest.Handled)
            {
                return;
            }
        }

        column.SetCurrentValue(
            WidthProperty,
            double.IsNaN(nativeWidth)
                ? GridLength.Auto
                : new GridLength(column.CoerceWidth(nativeWidth))
        );
#pragma warning restore WPF0020, WPF0022
    }

    private static void OnMinWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        column.CoerceValue(MaxWidthProperty);
        column.ApplyWidthDefinition(resetAutoMeasurement: true);
        column.LayoutPropertyChanged?.Invoke(column, EventArgs.Empty);
    }

    private static void OnMaxWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (GridViewColumn)d;
        column.ApplyWidthDefinition(resetAutoMeasurement: true);
        column.LayoutPropertyChanged?.Invoke(column, EventArgs.Empty);
    }

    /// <summary>
    /// Keeps the maximum width at least as large as the minimum and returns the boxed width.
    /// </summary>
    private static object CoerceMaxWidth(DependencyObject d, object? baseValue)
    {
        var column = (GridViewColumn)d;
        return Math.Max(column.MinWidth, (double)baseValue!);
    }

    private void ApplyWidthDefinition(bool resetAutoMeasurement)
    {
        if (Width.IsAuto)
        {
            if (resetAutoMeasurement && double.IsNaN(NativeWidth))
            {
                // Native GridView only re-enters its Init measurement state when Width changes.
                // Toggling the hidden double DP is the public-API equivalent of resetting Auto and
                // lets relaxed Min/Max constraints recover the uncropped content desired width.
                SetNativeWidth(0.0);
            }

            SetNativeWidth(double.NaN);
            return;
        }

        if (Width.IsAbsolute)
        {
            SetNativeWidth(CoerceWidth(Width.Value));
            return;
        }

        // A star definition must never be published to WPF as NaN. Native presenters interpret NaN
        // as content-sized Auto, which can expand a column for one layout pass and briefly expose a
        // horizontal scrollbar before the viewport coordinator contracts it. MinWidth is a stable,
        // finite placeholder until the owning ScrollViewer publishes its first viewport.
        SetNativeWidth(CoerceWidth(MinWidth));
    }

    private void SetNativeWidth(double value)
    {
        if (AreClose(NativeWidth, value))
        {
            return;
        }

        _isSynchronizingNativeWidth = true;
        try
        {
            SetCurrentValue(NativeGridViewColumn.WidthProperty, value);
        }
        finally
        {
            _isSynchronizingNativeWidth = false;
        }
    }

    private static bool ValidateMinWidth(object? value)
    {
        return value is double width && !double.IsNaN(width) && !double.IsInfinity(width) && width >= 0.0;
    }

    private static bool ValidateMaxWidth(object? value)
    {
        return value is double width && !double.IsNaN(width) && width >= 0.0;
    }

    private static bool AreClose(double first, double second)
    {
        if (double.IsNaN(first))
        {
            return double.IsNaN(second);
        }

        if (double.IsInfinity(first) || double.IsInfinity(second))
        {
            return first.Equals(second);
        }

        double delta = Math.Abs(first - second);
        return delta < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1.0);
    }
}
