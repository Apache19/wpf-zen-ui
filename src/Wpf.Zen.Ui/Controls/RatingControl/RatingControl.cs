// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents a nullable score as a configurable number of stars and optionally lets the user
/// select that score with the pointer or keyboard.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Rating"/> preserves finite values supplied by an application rather than silently
/// rounding them to <see cref="Precision"/>. Display rounding and range limiting are presentation
/// concerns. Values produced by user interaction are aligned to <see cref="Precision"/> and
/// constrained to the interval from zero through <see cref="MaximumRating"/>. Reducing
/// <see cref="MaximumRating"/> is the exception: an existing score above the new maximum is
/// immediately clamped so the control cannot retain an impossible score after its range changes.
/// </para>
/// <para>
/// A <see langword="null"/> rating represents an unrated item. In that state the control displays
/// <see cref="PlaceholderRating"/> without turning the placeholder into an actual score. The
/// template owns star layout through <c>PART_ItemsHost</c>; changing its items panel is therefore
/// sufficient to change spacing or arrangement without adding layout properties to this control.
/// </para>
/// <para>
/// <c>PART_ItemsHost</c> is the only template part and is optional for safe degradation. When it
/// is absent, the control cannot render stars or translate pointer positions, but dependency
/// properties, keyboard input, and <see cref="RatingChanged"/> continue to operate normally.
/// </para>
/// </remarks>
[TemplatePart(Name = ItemsHostPartName, Type = typeof(ItemsControl))]
public class RatingControl : Control
{
    private const string ItemsHostPartName = "PART_ItemsHost";

    private readonly ObservableCollection<RatingItem> _ratingItems = [];

    private ItemsControl? _itemsHost;
    private double?       _previewRating;

    static RatingControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RatingControl),
            new FrameworkPropertyMetadata(typeof(RatingControl))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RatingControl"/> class.
    /// </summary>
    public RatingControl()
    {
        RebuildRatingItems();
    }

    #region RatingProperty

    /// <summary>Identifies the <see cref="Rating"/> dependency property.</summary>
    public static readonly DependencyProperty RatingProperty = DependencyProperty.Register(
        nameof(Rating),
        typeof(double?),
        typeof(RatingControl),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            static (d, e) =>
            {
                var control = (RatingControl)d;
                control.RefreshRatingItems();
                control.OnRatingChanged(
                    new RoutedPropertyChangedEventArgs<double?>(
                        (double?)e.OldValue,
                        (double?)e.NewValue,
                        RatingChangedEvent
                    )
                );
            }
        ),
        static value => value is null || (value is double number && IsFinite(number))
    );

    /// <summary>
    /// Gets or sets the score represented by the control, or <see langword="null"/> when no score
    /// has been assigned.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. Finite values assigned directly are retained without
    /// precision rounding; only the star presentation is rounded and constrained. If
    /// <see cref="MaximumRating"/> is subsequently reduced below the current score, the score is
    /// clamped to that new maximum.
    /// </remarks>
    public double? Rating
    {
        get => (double?)GetValue(RatingProperty);
        set => SetValue(RatingProperty, value);
    }

    #endregion

    #region ItemTemplateProperty

    /// <summary>Identifies the <see cref="ItemTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.Register(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(RatingControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure)
    );

    /// <summary>
    /// Gets or sets the template used to display each <see cref="RatingItem"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default style supplies a Fluent star template. A custom template can bind to
    /// <see cref="RatingItem.State"/>, <see cref="RatingItem.DisplayMode"/>,
    /// <see cref="RatingItem.Fill"/>, and the other read-only item properties to present every
    /// fraction and score source independently.
    /// </para>
    /// <para>
    /// This property controls only item appearance. Arrangement, spacing, and direction remain
    /// responsibilities of the <c>PART_ItemsHost</c> items panel in the control template.
    /// </para>
    /// </remarks>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    #endregion

    #region MaximumRatingProperty

    /// <summary>Identifies the <see cref="MaximumRating"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumRatingProperty = DependencyProperty.Register(
        nameof(MaximumRating),
        typeof(int),
        typeof(RatingControl),
        new FrameworkPropertyMetadata(
            5,
            FrameworkPropertyMetadataOptions.AffectsMeasure,
            static (d, _) =>
            {
                var control = (RatingControl)d;
                control._previewRating = null;

                // MaximumRating defines the control's score range, not merely its item count.
                // SetCurrentValue preserves a Rating binding while publishing the clamped value
                // through it; assigning the CLR property here would replace that value source.
                if (control.Rating is { } rating && rating > control.MaximumRating)
                {
                    control.SetCurrentValue(RatingProperty, (double?)control.MaximumRating);
                }

                control.RebuildRatingItems();
            }
        ),
        validateValueCallback: static value => value is > 0
    );

    /// <summary>
    /// Gets or sets the number of stars displayed by the control.
    /// </summary>
    /// <remarks>
    /// The default is <c>5</c>. The value must be greater than zero. Reducing it clamps an existing
    /// <see cref="Rating"/> to the new maximum while leaving <see cref="PlaceholderRating"/>
    /// unchanged.
    /// </remarks>
    public int MaximumRating
    {
        get => (int)GetValue(MaximumRatingProperty);
        set => SetValue(MaximumRatingProperty, value);
    }

    #endregion

    #region PrecisionProperty

    /// <summary>Identifies the <see cref="Precision"/> dependency property.</summary>
    public static readonly DependencyProperty PrecisionProperty = DependencyProperty.Register(
        nameof(Precision),
        typeof(RatingPrecision),
        typeof(RatingControl),
        new FrameworkPropertyMetadata(
            RatingPrecision.FullStar,
            FrameworkPropertyMetadataOptions.AffectsRender,
            static (d, _) =>
            {
                var control = (RatingControl)d;
                control._previewRating = null;
                control.RefreshRatingItems();
            }
        ),
        static value => value is RatingPrecision precision
                     && Enum.IsDefined(typeof(RatingPrecision), precision)
    );

    /// <summary>
    /// Gets or sets the smallest star fraction used for presentation and user interaction.
    /// </summary>
    /// <remarks>The default is <see cref="RatingPrecision.FullStar"/>.</remarks>
    public RatingPrecision Precision
    {
        get => (RatingPrecision)GetValue(PrecisionProperty);
        set => SetValue(PrecisionProperty, value);
    }

    #endregion

    #region IsClearEnabledProperty

    /// <summary>Identifies the <see cref="IsClearEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsClearEnabledProperty = DependencyProperty.Register(
        nameof(IsClearEnabled),
        typeof(bool),
        typeof(RatingControl),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether selecting the current rating clears it.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. When enabled, selecting the same score a second time
    /// assigns <see langword="null"/> rather than zero. This behavior applies only when the control
    /// is not read-only.
    /// </remarks>
    public bool IsClearEnabled
    {
        get => (bool)GetValue(IsClearEnabledProperty);
        set => SetValue(IsClearEnabledProperty, value);
    }

    #endregion

    #region PlaceholderRatingProperty

    /// <summary>Identifies the <see cref="PlaceholderRating"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderRatingProperty = DependencyProperty.Register(
        nameof(PlaceholderRating),
        typeof(double),
        typeof(RatingControl),
        new FrameworkPropertyMetadata(
            0.0,
            FrameworkPropertyMetadataOptions.AffectsRender,
            static (d, _) => ((RatingControl)d).RefreshRatingItems()
        ),
        static value => value is double number && IsFinite(number)
    );

    /// <summary>
    /// Gets or sets the score displayed while <see cref="Rating"/> is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The default is <c>0</c>. Like <see cref="Rating"/>, this value is retained as supplied while
    /// its presentation is rounded and constrained to the visible star range.
    /// </remarks>
    public double PlaceholderRating
    {
        get => (double)GetValue(PlaceholderRatingProperty);
        set => SetValue(PlaceholderRatingProperty, value);
    }

    #endregion

    #region IsReadOnlyProperty

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(RatingControl),
        new PropertyMetadata(
            false,
            static (d, e) =>
            {
                if ((bool)e.NewValue)
                {
                    var control = (RatingControl)d;
                    control._previewRating = null;
                    control.RefreshRatingItems();
                }
            }
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether pointer and keyboard changes are disabled.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. A read-only control remains focusable so that its
    /// score can still be reached by keyboard and accessibility users.
    /// </remarks>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    #endregion

    #region RatingChangedEvent

    /// <summary>Identifies the <see cref="RatingChanged"/> routed event.</summary>
    public static readonly RoutedEvent RatingChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(RatingChanged),
        RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<double?>),
        typeof(RatingControl)
    );

    /// <summary>
    /// Occurs when <see cref="Rating"/> changes, whether the change came from input, a binding, or
    /// direct property assignment.
    /// </summary>
    public event RoutedPropertyChangedEventHandler<double?> RatingChanged
    {
        add => AddHandler(RatingChangedEvent, value);
        remove => RemoveHandler(RatingChangedEvent, value);
    }

    #endregion

    /// <summary>
    /// Reconnects the control to the items host supplied by its current template.
    /// </summary>
    /// <remarks>
    /// The items host is template-owned and therefore cannot be resolved before this lifecycle
    /// point. Reapplying a template first releases the previous host so it cannot retain the
    /// presentation collection after its visual tree has been replaced.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        _itemsHost?.ClearValue(ItemsControl.ItemsSourceProperty);

        base.OnApplyTemplate();

        _itemsHost = GetTemplateChild(ItemsHostPartName) as ItemsControl;

        // PART_ItemsHost is owned by the control contract. Supplying the collection here keeps
        // layout entirely in the template while avoiding a public presentation-only property.
        _itemsHost?.SetCurrentValue(ItemsControl.ItemsSourceProperty, _ratingItems);

        RefreshRatingItems();
    }

    /// <summary>
    /// Raises <see cref="RatingChanged"/> for a completed dependency-property transition.
    /// </summary>
    /// <param name="e">The old and new rating values.</param>
    protected virtual void OnRatingChanged(RoutedPropertyChangedEventArgs<double?> e)
    {
        RaiseEvent(e);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (IsReadOnly)
        {
            return;
        }

        if (TryGetRatingFromPointer(e, out var rating))
        {
            _previewRating = rating;
            RefreshRatingItems();
        }
        else if (_previewRating is not null)
        {
            // A custom template may stretch the control beyond its items host. Clear the preview
            // as soon as the pointer enters that non-rating area instead of waiting for the pointer
            // to leave the outer control entirely.
            _previewRating = null;
            RefreshRatingItems();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        _previewRating = null;
        RefreshRatingItems();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        Focus();

        if (IsReadOnly || !TryGetRatingFromPointer(e, out var rating))
        {
            return;
        }

        SetRatingFromInteraction(rating);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (IsReadOnly)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
                // Key values describe physical keys and are not mirrored by WPF. Keep horizontal
                // keyboard navigation aligned with the visual item order: in RTL, moving right
                // approaches the low-rating edge. Vertical keys retain their score semantics.
                StepRating(FlowDirection == FlowDirection.RightToLeft ? -1 : 1);
                e.Handled = true;
                break;

            case Key.Left:
                // Mirrors Key.Right: in RTL layout, moving left approaches the high-rating edge.
                StepRating(FlowDirection == FlowDirection.RightToLeft ? 1 : -1);
                e.Handled = true;
                break;

            case Key.Up:
                StepRating(1);
                e.Handled = true;
                break;

            case Key.Down:
                StepRating(-1);
                e.Handled = true;
                break;

            case Key.Home:
                SetRatingFromKeyboard(0);
                e.Handled = true;
                break;

            case Key.End:
                SetRatingFromKeyboard(MaximumRating);
                e.Handled = true;
                break;

            case Key.Delete when IsClearEnabled:
                SetCurrentValue(RatingProperty, null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Applies a score created by pointer or keyboard interaction.
    /// </summary>
    /// <remarks>
    /// SetCurrentValue is required here: WPF keeps the property's existing binding or other value
    /// source while publishing the user's edit back through it. Unlike source-provided values,
    /// interactive values are always aligned and constrained before being assigned.
    /// </remarks>
    internal void SetRatingFromInteraction(double rating)
    {
        if (IsReadOnly)
        {
            return;
        }

        double interactiveRating = NormalizeInteractiveRating(rating);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        double? newRating = IsClearEnabled && Rating == interactiveRating
            ? null
            : interactiveRating;

        _previewRating = null;

        SetCurrentValue(RatingProperty, newRating);
        RefreshRatingItems();
    }

    /// <summary>
    /// Produces the score shown for an arbitrary finite source value without mutating that source.
    /// </summary>
    internal double GetDisplayedRating(double rating)
    {
        var constrained = Math.Max(0, Math.Min(MaximumRating, rating));
        var step = GetPrecisionStep();
        return Math.Min(
            MaximumRating,
            Math.Round(constrained / step, MidpointRounding.AwayFromZero) * step
        );
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private void RebuildRatingItems()
    {
        _ratingItems.Clear();

        for (var index = 0; index < MaximumRating; index++)
        {
            _ratingItems.Add(new RatingItem(index));
        }

        RefreshRatingItems();
    }

    private void RefreshRatingItems()
    {
        var sourceRating = _previewRating ?? Rating ?? PlaceholderRating;
        var displayedRating = GetDisplayedRating(sourceRating);
        var displayMode = _previewRating is not null
            ? RatingItemDisplayMode.Preview
            : Rating is not null
                ? RatingItemDisplayMode.Rating
                : RatingItemDisplayMode.Placeholder;

        for (var index = 0; index < _ratingItems.Count; index++)
        {
            var fill = Math.Max(0, Math.Min(1, displayedRating - index));
            _ratingItems[index].Update(fill, displayMode);
        }
    }

    private void StepRating(int direction)
    {
        // Keyboard adjustment forms a bounded sequence whose lowest state is null, followed by
        // zero and then each precision step. It intentionally does not call
        // SetRatingFromInteraction: that method implements repeated pointer selection clearing,
        // which would otherwise make a held arrow key wrap at either end of the sequence.
        if (Rating is null)
        {
            if (direction > 0)
            {
                SetRatingFromKeyboard(0);
            }
            else
            {
                SetRatingFromKeyboard(null);
            }

            return;
        }

        var displayedRating = GetDisplayedRating(Rating.Value);
        if (direction < 0 && displayedRating <= 0)
        {
            SetRatingFromKeyboard(null);
            return;
        }

        if (direction > 0 && displayedRating >= MaximumRating)
        {
            SetRatingFromKeyboard(MaximumRating);
            return;
        }

        var steppedRating = displayedRating + (direction * GetPrecisionStep());
        SetRatingFromKeyboard(NormalizeInteractiveRating(steppedRating));
    }

    private void SetRatingFromKeyboard(double? rating)
    {
        // Keyboard edits must not inherit IsClearEnabled's repeated pointer-selection toggle.
        // Refresh explicitly because assigning an endpoint that is already current does not invoke
        // the dependency-property callback, but must still dismiss a pointer preview.
        _previewRating = null;
        SetCurrentValue(RatingProperty, rating);
        RefreshRatingItems();
    }

    private double NormalizeInteractiveRating(double rating)
    {
        var constrained = Math.Max(0, Math.Min(MaximumRating, rating));
        var step = GetPrecisionStep();
        return Math.Min(
            MaximumRating,
            Math.Round(constrained / step, MidpointRounding.AwayFromZero) * step
        );
    }

    private double GetPrecisionStep()
    {
        return Precision switch
        {
            RatingPrecision.QuarterStar => 0.25,
            RatingPrecision.HalfStar    => 0.5,
            _                           => 1.0,
        };
    }

    private bool TryGetRatingFromPointer(MouseEventArgs e, out double rating)
    {
        rating = 0;

        if (_itemsHost is null)
        {
            return false;
        }

        var hitElement = _itemsHost.InputHitTest(e.GetPosition(_itemsHost));

        // ReSharper disable once SuspiciousTypeConversion.Global
        if (hitElement is not DependencyObject hitObject
            || _itemsHost.ContainerFromElement(hitObject) is not FrameworkElement container
            || container.ActualWidth <= 0)
        {
            return false;
        }

        var index = _itemsHost.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            return false;
        }

        rating = GetRatingFromItemPosition(
            index,
            e.GetPosition(container).X,
            container.ActualWidth
        );

        return true;
    }

    /// <summary>
    /// Converts an item-local horizontal coordinate into an interactive score.
    /// </summary>
    /// <remarks>
    /// <paramref name="localX"/> must use the coordinate system returned by WPF for the item
    /// container. A <see cref="FlowDirection.RightToLeft"/> mirror is already represented by
    /// that coordinate system and must not be applied again here.
    /// </remarks>
    internal double GetRatingFromItemPosition(int index, double localX, double actualWidth)
    {
        var fraction = Math.Max(0, Math.Min(1, localX / actualWidth));
        var step = GetPrecisionStep();

        // A star's first segment represents the smallest selectable non-zero score. Subtracting a
        // tiny tolerance keeps an exact visual boundary in the segment on its left despite layout
        // transform noise (for example, 0.50000000001 at the half-star boundary).
        var segment = Math.Ceiling((fraction - 1e-10) / step) * step;
        return NormalizeInteractiveRating(index + Math.Max(step, segment));
    }
}
