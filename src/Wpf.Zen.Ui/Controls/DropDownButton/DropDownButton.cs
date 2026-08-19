// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A control that drop downs a flyout of choices from which one can be chosen.
/// </summary>
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

    private ContextMenu? _contextMenu;

    /// <summary>Identifies the <see cref="Flyout"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutProperty = DependencyProperty.Register(
        nameof(Flyout),
        typeof(object),
        typeof(DropDownButton),
        new PropertyMetadata(null, OnFlyoutChanged)
    );

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(DropDownButton),
        new PropertyMetadata(false, OnIsDropDownOpenChanged)
    );

    /// <summary>Identifies the <see cref="IsDropDownArrowVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownArrowVisibleProperty = DependencyProperty.Register(
        nameof(IsDropDownArrowVisible),
        typeof(bool),
        typeof(DropDownButton),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets the flyout associated with this button.
    /// </summary>
    [Bindable(true)]
    public object? Flyout
    {
        get => GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the drop-down for a button is currently open.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if the drop-down is open; otherwise, <see langword="false" />. The default is <see langword="false" />.</returns>
    [Bindable(true)]
    [Browsable(false)]
    [Category("Appearance")]
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

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
            dropDownButton.OnFlyoutChanged(e.NewValue);
        }
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownButton dropDownButton)
        {
            dropDownButton.OnIsDropDownOpenChanged((bool)e.NewValue);
        }
    }

    /// <summary>This method is invoked when the <see cref="FlyoutProperty"/> changes.</summary>
    /// <param name="value">The new value of <see cref="FlyoutProperty"/>.</param>
    protected virtual void OnFlyoutChanged(object value)
    {
        if (value is ContextMenu contextMenu)
        {
            _contextMenu        =  contextMenu;
            _contextMenu.Opened += OnContextMenuOpened;
            _contextMenu.Closed += OnContextMenuClosed;
        }
    }

    protected virtual void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    protected virtual void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    /// <summary>This method is invoked when the <see cref="IsDropDownOpenProperty"/> changes.</summary>
    /// <param name="currentValue">The new value of <see cref="IsDropDownOpenProperty"/>.</param>
    protected virtual void OnIsDropDownOpenChanged(bool currentValue) { }

    protected override void OnClick()
    {
        base.OnClick();

        // Displays the menu within the click event handler
        // to allow the ClickMode property to control the timing of the menu popup.
        if (_contextMenu is null)
        {
            return;
        }

        // Sets MinWidth only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper
               .GetValueSource(_contextMenu, System.Windows.Controls.ContextMenu.MinWidthProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            _contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.MinWidthProperty, ActualWidth);
        }

        // Sets PlacementTarget only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper
               .GetValueSource(_contextMenu, System.Windows.Controls.ContextMenu.PlacementTargetProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            _contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.PlacementTargetProperty, this);
        }

        // Sets Placement only when it has not been explicitly set by the user.
        // When no explicit value is provided, the menu is displayed below the button by default.
        if (DependencyPropertyHelper
               .GetValueSource(_contextMenu, System.Windows.Controls.ContextMenu.PlacementProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            _contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.PlacementProperty, PlacementMode.Bottom);
        }

        _contextMenu.SetCurrentValue(System.Windows.Controls.ContextMenu.IsOpenProperty, true);
    }
}
