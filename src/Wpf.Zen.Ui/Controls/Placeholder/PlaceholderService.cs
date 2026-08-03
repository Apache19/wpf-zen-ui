// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using NativeComboBox = System.Windows.Controls.ComboBox;
using NativeTextBox = System.Windows.Controls.TextBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

// TODO:
// PlaceholderService could potentially be extended to support more control types.
// For example, it may check whether a ContentControl's Content is empty
// and display placeholder content accordingly.
// This enhancement can be evaluated and implemented if there is a concrete need.

public static class PlaceholderService
{
    private const string PlaceholderPartName = "PART_Placeholder";

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.RegisterAttached(
            "Content",
            typeof(object),
            typeof(PlaceholderService),
            new PropertyMetadata(null, OnContentChanged)
        );

    /// <summary>Helper for setting <see cref="ContentProperty"/> on <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to set <see cref="ContentProperty"/> on.</param>
    /// <param name="value">Content property value.</param>
    public static void SetContent(DependencyObject element, object? value)
    {
        element.SetValue(ContentProperty, value);
    }

    /// <summary>Helper for getting <see cref="ContentProperty"/> from <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to read <see cref="ContentProperty"/> from.</param>
    /// <returns>Content property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static object? GetContent(DependencyObject element)
    {
        return element.GetValue(ContentProperty);
    }

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Control control)
        {
            if (control.IsLoaded)
            {
                // Try to unsubscribe as much as possible.
                control.Loaded -= Control_Loaded;
                InitializeControl(control);
            }
            else
            {
                control.Loaded -= Control_Loaded;
                control.Loaded += Control_Loaded;
            }
        }
    }

    private static void Control_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.Loaded -= Control_Loaded;
        InitializeControl(control);
    }

    private static void InitializeControl(Control control)
    {
        UpdateVisibility(control);

        switch (control)
        {
            case NativeTextBox textBox:
            {
                textBox.TextChanged -= OnTextChanged;
                textBox.TextChanged += OnTextChanged;
                break;
            }

            case NativeComboBox comboBox:
                comboBox.SelectionChanged -= OnComboSelectionChanged;
                comboBox.SelectionChanged += OnComboSelectionChanged;
                comboBox.RemoveHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
                comboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnTextChanged));
                break;
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateVisibility(control);
        }
    }

    private static void OnComboSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateVisibility(control);
        }
    }

    private static void UpdateVisibility(Control control)
    {
        var part = control.Template?.FindName(PlaceholderPartName, control);
        if (part is not PlaceholderControl placeholder)
        {
            return;
        }

        // Check binding first to avoid overwriting it
        if (BindingOperations.GetBinding(placeholder, ContentControl.ContentProperty) == null)
        {
            placeholder.SetCurrentValue(ContentControl.ContentProperty, GetContent(control));
        }

        bool isEmpty;
        switch (control)
        {
            case NativeTextBox tb:
            {
                isEmpty = string.IsNullOrEmpty(tb.Text);
                break;
            }

            case NativeComboBox cb:
            {
                var editableTextBox = cb.Template?.FindName("PART_EditableTextBox", cb) as NativeTextBox;
                isEmpty = cb.SelectedItem == null && string.IsNullOrEmpty(editableTextBox?.Text ?? cb.Text);
                break;
            }

            default:
            {
                isEmpty = true;
                break;
            }
        }

        placeholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}