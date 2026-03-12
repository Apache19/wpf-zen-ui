// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

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
            case TextBox textBox:
                textBox.TextChanged -= TextChanged;
                textBox.TextChanged += TextChanged;
                break;

            case ComboBox comboBox:
                comboBox.SelectionChanged -= ComboSelectionChanged;
                comboBox.SelectionChanged += ComboSelectionChanged;
                break;
        }
    }

    private static void TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Control control)
        {
            UpdateVisibility(control);
        }
    }

    private static void ComboSelectionChanged(object sender, SelectionChangedEventArgs e)
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

        // todo Forcibly setting the content seems inappropriate, as it doesn't consider Binding."
        placeholder.Content = GetContent(control);

        bool isEmpty = control switch
        {
            TextBox tb  => string.IsNullOrEmpty(tb.Text),
            ComboBox cb => cb.SelectedItem == null && string.IsNullOrEmpty(cb.Text),
            _           => true
        };

        placeholder.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}