// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Owns transient surfaces independently of the measured key and header regions.</summary>
[TemplatePart(Name = "PART_AlternatePopup", Type = typeof(Popup))]
public partial class SoftKeyboard
{
    private ButtonBase? _heldButton;
    private Popup? _alternatePopup;
    private ContextMenu? _inputMethodMenu;

    /// <summary>Identifies the header content property.</summary>
    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.Register(
        nameof(HeaderContent), typeof(object), typeof(SoftKeyboard), new PropertyMetadata(null));

    /// <summary>Gets or sets content displayed in the reserved header when no candidates are present. Defaults to null.</summary>
    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    /// <summary>Identifies the header data template property.</summary>
    public static readonly DependencyProperty HeaderContentTemplateProperty = DependencyProperty.Register(
        nameof(HeaderContentTemplate), typeof(DataTemplate), typeof(SoftKeyboard), new PropertyMetadata(null));

    /// <summary>Gets or sets the optional template for idle header content. It does not change the reserved height.</summary>
    public DataTemplate? HeaderContentTemplate
    {
        get => (DataTemplate?)GetValue(HeaderContentTemplateProperty);
        set => SetValue(HeaderContentTemplateProperty, value);
    }

    /// <summary>Identifies the reserved header height property.</summary>
    public static readonly DependencyProperty HeaderHeightProperty = DependencyProperty.Register(
        nameof(HeaderHeight),
        typeof(double),
        typeof(SoftKeyboard),
        new FrameworkPropertyMetadata(48d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        static value => value is double height && !double.IsNaN(height) && !double.IsInfinity(height) && height >= 44);

    /// <summary>Gets or sets the header height in DIP. Defaults to 48; finite values of at least 44 preserve touch targets.</summary>
    /// <remarks>This non-inherited, animatable value affects measurement. Candidate visibility never changes it.</remarks>
    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    /// <summary>Connects the optional PART_AlternatePopup after a template has created its visual tree.</summary>
    /// <remarks>Omitting this part disables long-press alternatives without affecting ordinary key input.</remarks>
    public override void OnApplyTemplate()
    {
        if (_alternatePopup is not null)
        {
            _alternatePopup.SetCurrentValue(Popup.IsOpenProperty, false);
            _alternatePopup.Closed -= OnAlternatePopupClosed;
            _alternatePopup.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }

        base.OnApplyTemplate();
        _alternatePopup = GetTemplateChild("PART_AlternatePopup") as Popup;
        if (_alternatePopup is not null)
        {
            _alternatePopup.Closed += OnAlternatePopupClosed;
            // Popup has a separate presentation source. Handle selection here rather than relying
            // on the host control being in the routed event path of a replacement template.
            _alternatePopup.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }
    }

    private bool OpenAlternates()
    {
        if (_alternatePopup is null)
        {
            _alternateValues.Clear();
            return false;
        }

        _alternatePopup.SetCurrentValue(Popup.PlacementTargetProperty, _heldButton ?? (UIElement)this);
        SetValue(IsAlternatePopupOpenPropertyKey, true);
        // Keep the originating ButtonBase capture until release. Auto-close capture would treat
        // that release below the popup as an outside click and immediately dismiss the variants.
        _alternatePopup.SetCurrentValue(Popup.StaysOpenProperty, true);
        _alternatePopup.SetCurrentValue(Popup.IsOpenProperty, true);
        return true;
    }

    private void OnAlternatePopupClosed(object? sender, EventArgs eventArgs)
    {
        SetValue(IsAlternatePopupOpenPropertyKey, false);
        _alternateValues.Clear();
        _heldButton = null;
    }

    /// <summary>Opens the installed input-method menu without replacing or resizing the keyboard.</summary>
    /// <remarks>The menu is anchored to the last invoked key. Selection restores editor focus before activation.</remarks>
    public void ShowInputMethodMenu()
    {
        if (!IsEnabled || !IsOpen || !IsVisible)
        {
            return;
        }

        RunAfterActiveCompositionCancellation(OpenInputMethodMenuCore);
    }

    private void OpenInputMethodMenuCore()
    {
        if (!IsLoaded || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        CloseFlyouts();
        RefreshInputMethods();
        var menu = new ContextMenu { PlacementTarget = _heldButton ?? (UIElement)this, Placement = PlacementMode.Top };
        foreach (SoftKeyboardInputMethod inputMethod in InputMethods)
        {
            var item = new MenuItem { Header = inputMethod.DisplayName, IsCheckable = true, IsChecked = inputMethod.IsActive };
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
