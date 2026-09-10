// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides header presentation and the keyboard-wide input-method menu.
/// </summary>
public partial class SoftKeyboard
{
    private ContextMenu? _inputMethodMenu;

    /// <summary>
    /// Identifies the <see cref="HeaderContent"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HeaderContentProperty
        = DependencyProperty.Register(
            nameof(HeaderContent),
            typeof(object),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets content displayed in the reserved header when no candidates are present.
    /// Defaults to null.
    /// </summary>
    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HeaderContentTemplate"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HeaderContentTemplateProperty
        = DependencyProperty.Register(
            nameof(HeaderContentTemplate),
            typeof(DataTemplate),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the optional template for idle header content.
    /// It does not change the reserved height.
    /// </summary>
    public DataTemplate? HeaderContentTemplate
    {
        get => (DataTemplate?)GetValue(HeaderContentTemplateProperty);
        set => SetValue(HeaderContentTemplateProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HeaderHeight"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HeaderHeightProperty
        = DependencyProperty.Register(
            nameof(HeaderHeight),
            typeof(double),
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(
                48d, FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            static value => value is double height && !double.IsNaN(height)
                            && !double.IsInfinity(height) && height >= 44
        );

    /// <summary>
    /// Gets or sets the header height in DIP.
    /// Defaults to 48; finite values of at least 44 preserve touch targets.
    /// </summary>
    /// <remarks>
    /// This non-inherited, animatable value affects measurement.
    /// Candidate visibility never changes it.
    /// </remarks>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    /// <summary>
    /// Opens the installed input-method menu without replacing or resizing the keyboard.
    /// </summary>
    /// <remarks>
    /// The menu is anchored to the last invoked key.
    /// Selection restores editor focus before activation.
    /// </remarks>
    public void ShowInputMethodMenu()
    {
        if (!IsEnabled || !IsOpen || !IsVisible)
        {
            return;
        }

        UIElement anchor = _inputMethodMenuAnchor ?? this;
        RunAfterActiveCompositionCancellation(() => OpenInputMethodMenuCore(anchor));
    }

    private void OpenInputMethodMenuCore(UIElement anchor)
    {
        if (!IsLoaded || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        CloseFlyouts();
        RefreshInputMethods();

        var menu = new ContextMenu
        {
            PlacementTarget = anchor,
            Placement = PlacementMode.Top
        };

        foreach (SoftKeyboardInputMethod inputMethod in InputMethods)
        {
            var item = new MenuItem
            {
                Header = inputMethod.DisplayName,
                IsCheckable = true,
                IsChecked = inputMethod.IsActive
            };

            item.Click += (_, _) =>
            {
                // Close the menu first: focus restoration and composition cancellation must target
                // the editor's TSF document, not the menu's temporary focus scope.
                menu.IsOpen = false;
                if (PrepareTargetForInput())
                {
                    ActivateInputMethod(inputMethod);
                }
            };

            menu.Items.Add(item);
        }

        _inputMethodMenu = menu;
        menu.IsOpen = menu.Items.Count > 0;
    }
}
