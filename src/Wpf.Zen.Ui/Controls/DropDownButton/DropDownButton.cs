// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A button that opens a ContextMenu or a Popup after raising Click and executing its command.
/// </summary>
/// <remarks>
/// Native Button input, access keys, and ClickMode determine activation timing. The default
/// template adds an optional chevron and uses Button's resolved interaction brushes. No named
/// template part is required for flyout operation. Replacing Flyout detaches and closes the old
/// flyout, then adopts the new flyout's open state. A null Flyout leaves a normal button.
/// Popup content owns its layout, focus, and keyboard behavior; it does not acquire menu semantics.
/// </remarks>
public class DropDownButton : Button
{
    static DropDownButton()
    {
        // DropDownButton owns the trailing-chevron contract, so it needs its own default template
        // instead of forcing every Button to carry a second public icon slot.
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DropDownButton),
            new FrameworkPropertyMetadata(typeof(DropDownButton))
        );
    }

    private bool _isSynchronizingDropDownState;

    /// <summary>Initializes a new instance of the <see cref="DropDownButton"/> class.</summary>
    public DropDownButton()
    {
        // A Flyout Popup need not belong to the visual tree, so WPF's Popup.Unloaded handling
        // alone cannot close it when navigation removes the owning button.
        Unloaded += (_, _) => SetCurrentValue(IsDropDownOpenProperty, false);
    }

    /// <summary>Identifies the <see cref="Flyout"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutProperty = DependencyProperty.Register(
        nameof(Flyout),
        typeof(object),
        typeof(DropDownButton),
        new PropertyMetadata(null, OnFlyoutChanged),
        value => value is null or System.Windows.Controls.ContextMenu or Popup
    );

    /// <summary>Gets or sets the ContextMenu or Popup opened by this button. The default is null.</summary>
    /// <remarks>
    /// Validation rejects values other than ContextMenu, Popup, or null. Put custom panel content
    /// inside Popup.Child. The callback transfers event subscriptions and synchronizes IsDropDownOpen. This
    /// property supports binding, does not inherit, and does not directly invalidate layout.
    /// When opening a Popup, unset PlacementTarget, Placement, and StaysOpen values default to
    /// this button, Bottom, and false respectively. Explicit values, styles, and bindings are retained.
    /// Popup content determines its own size; ContextMenu defaults to at least the button's width.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public object? Flyout
    {
        get => GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(DropDownButton),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsDropDownOpenChanged
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the drop-down for a button is currently open.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if the drop-down is open; otherwise, <see langword="false" />. The default is <see langword="false" />.</returns>
    /// <remarks>
    /// Binds two-way by default and synchronizes popup opening and dismissal without replacing
    /// application bindings. Opening without a flyout resets this value to false. The property
    /// does not inherit or invalidate button layout; its callback operates the popup.
    /// </remarks>
    [Bindable(true)]
    [Browsable(false)]
    [Category("Appearance")]
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>Identifies the <see cref="IsDropDownArrowVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownArrowVisibleProperty = DependencyProperty.Register(
        nameof(IsDropDownArrowVisible),
        typeof(bool),
        typeof(DropDownButton),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the control displays the drop-down arrow.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public bool IsDropDownArrowVisible
    {
        get => (bool)GetValue(IsDropDownArrowVisibleProperty);
        set => SetValue(IsDropDownArrowVisibleProperty, value);
    }

    private static void OnFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownButton dropDownButton)
        {
            dropDownButton.OnFlyoutChanged(e.OldValue, e.NewValue);
        }
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownButton dropDownButton)
        {
            dropDownButton.OnIsDropDownOpenChanged((bool)e.NewValue);
        }
    }

    /// <summary>Transfers flyout subscriptions and adopts the new flyout's open state.</summary>
    /// <param name="oldValue">The ContextMenu or Popup to detach and close, or null.</param>
    /// <param name="newValue">The ContextMenu or Popup whose state should be observed, or null.</param>
    protected virtual void OnFlyoutChanged(object? oldValue, object? newValue)
    {
        // Detach before closing: Closed can arrive asynchronously and must not reset the
        // replacement's state or keep this button subscribed to a detached flyout.
        switch (oldValue)
        {
            case ContextMenu menu:
                menu.Opened -= OnContextMenuOpened;
                menu.Closed -= OnContextMenuClosed;
                menu.SetCurrentValue(ContextMenu.IsOpenProperty, false);
                break;
            case Popup popup:
                popup.Opened -= OnPopupOpened;
                popup.Closed -= OnPopupClosed;
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                break;
        }

        switch (newValue)
        {
            case ContextMenu menu:
                menu.Opened += OnContextMenuOpened;
                menu.Closed += OnContextMenuClosed;
                break;
            case Popup popup:
                popup.Opened += OnPopupOpened;
                popup.Closed += OnPopupClosed;
                break;
        }

        UpdateDropDownStateFromFlyout(newValue switch
        {
            ContextMenu menu => menu.IsOpen,
            Popup popup => popup.IsOpen,
            _ => false,
        });
    }

    /// <summary>Synchronizes popup dismissal back to the button and its binding source.</summary>
    /// <param name="sender">The menu that raised the event.</param>
    /// <param name="e">The routed close event.</param>
    protected virtual void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, Flyout))
        {
            UpdateDropDownStateFromFlyout(false);
        }
    }

    /// <summary>Synchronizes popup opening back to the button and its binding source.</summary>
    /// <param name="sender">The menu that raised the event.</param>
    /// <param name="e">The routed open event.</param>
    protected virtual void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, Flyout))
        {
            UpdateDropDownStateFromFlyout(true);
        }
    }

    // Popup exposes CLR events rather than ContextMenu's routed events. Both feed the same
    // guarded state update so dismissal preserves IsDropDownOpen bindings without reopening.
    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Flyout))
        {
            UpdateDropDownStateFromFlyout(true);
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Flyout))
        {
            UpdateDropDownStateFromFlyout(false);
        }
    }

    /// <summary>This method is invoked when the <see cref="IsDropDownOpenProperty"/> changes.</summary>
    /// <param name="currentValue">The new value of <see cref="IsDropDownOpenProperty"/>.</param>
    protected virtual void OnIsDropDownOpenChanged(bool currentValue)
    {
        if (_isSynchronizingDropDownState)
        {
            return;
        }

        if (Flyout is null)
        {
            if (currentValue)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }

            return;
        }

        switch (Flyout)
        {
            case ContextMenu menu:
                if (currentValue)
                {
                    ConfigureContextMenu(menu);
                }

                menu.SetCurrentValue(ContextMenu.IsOpenProperty, currentValue);
                break;
            case Popup popup:
                if (currentValue)
                {
                    ConfigurePopup(popup);
                }

                popup.SetCurrentValue(Popup.IsOpenProperty, currentValue);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    /// <summary>Adopts flyout events without issuing another popup operation from the DP callback.</summary>
    private void UpdateDropDownStateFromFlyout(bool isOpen)
    {
        _isSynchronizingDropDownState = true;
        try
        {
            SetCurrentValue(IsDropDownOpenProperty, isOpen);
        }
        finally
        {
            // Binding source setters can run application code and throw during propagation.
            _isSynchronizingDropDownState = false;
        }
    }

    /// <summary>Supplies drop-down defaults without overwriting application configuration.</summary>
    private void ConfigurePopup(Popup popup)
    {
        if (DependencyPropertyHelper.GetValueSource(popup, Popup.PlacementTargetProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.PlacementTargetProperty, this);
        }

        if (DependencyPropertyHelper.GetValueSource(popup, Popup.PlacementProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.PlacementProperty, PlacementMode.Bottom);
        }

        // Native Popup defaults to StaysOpen=true. A button flyout normally dismisses on an
        // outside click; WPF owns capture and dismissal once this property is false.
        if (DependencyPropertyHelper.GetValueSource(popup, Popup.StaysOpenProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.StaysOpenProperty, false);
        }
    }

    /// <summary>Supplies placement defaults while retaining local values, styles, and bindings.</summary>
    private void ConfigureContextMenu(ContextMenu contextMenu)
    {
        // Sets MinWidth only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, System.Windows.Controls.ContextMenu.MinWidthProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.MinWidthProperty, ActualWidth);
        }

        // Sets PlacementTarget only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, System.Windows.Controls.ContextMenu.PlacementTargetProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.PlacementTargetProperty, this);
        }

        // Sets Placement only when it has not been explicitly set by the user.
        // When no explicit value is provided, the menu is displayed below the button by default.
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, System.Windows.Controls.ContextMenu.PlacementProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.PlacementProperty, PlacementMode.Bottom);
        }
    }
}
