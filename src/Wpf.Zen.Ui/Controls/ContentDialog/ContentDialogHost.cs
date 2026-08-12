// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable IDE0008 // Use explicit type instead of 'var'

/// <summary>
/// Provides a host control for displaying modal content dialogs within a WPF visual tree.
/// </summary>
/// <example>
/// <para>XAML (place near the root of the Window):</para>
/// <code lang="xml">
/// &lt;Window x:Class="MyApp.MainWindow" xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"&gt;
///   &lt;Grid&gt;
///     &lt;ui:ContentDialogHost x:Name="RootDialogHost" /&gt;
///   &lt;/Grid&gt;
/// &lt;/Window&gt;
/// </code>
/// <para>C# (showing a simple dialog via the host):</para>
/// <code lang="csharp">
/// var dialog = new ContentDialog(RootDialogHost)
/// {
///     Title = "Confirm",
///     Content = "Are you sure?",
///     PrimaryButtonText = "Yes",
///     CloseButtonText = "No",
/// };
///
/// var result = await dialog.ShowAsync();
/// </code>
/// </example>
/// <remarks>
/// <para>
/// Use this control to present modal dialogs that overlay application content
/// and optionally disable interaction with sibling elements.
/// </para>
/// <para>
/// <strong>Placement Requirements:</strong>
/// 1. Place near the root of the UI scope that the dialog should cover.
/// 2. Position as the last sibling among its peers to guarantee the highest Z-order.
/// </para>
/// <para>
/// A host is not globally registered against its containing Window. Consumers pass the intended
/// instance directly to a dialog or presentation-scoped service, which also supports ElementHost,
/// HwndSource, plug-in, and multiple independently composed regions in one native window.
/// </para>
/// </remarks>
public class ContentDialogHost : ContentControl
{
    /// <summary>Identifies the <see cref="IsDisableSiblingsEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsDisableSiblingsEnabledProperty = DependencyProperty.Register(
        nameof(IsDisableSiblingsEnabled),
        typeof(bool),
        typeof(ContentDialogHost),
        new PropertyMetadata(false, OnIsDisableSiblingsEnabledChanged)
    );

    private readonly ContentDialogHostController _controller;

    static ContentDialogHost()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialogHost),
            new FrameworkPropertyMetadata(typeof(ContentDialogHost))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogHost"/> class.
    /// </summary>
    public ContentDialogHost()
    {
        _controller = new ContentDialogHostController(this);
    }

    /// <summary>
    /// Gets or sets a value indicating whether sibling elements of the dialog host should be
    /// disabled while the dialog is displayed. The default value is <see langword="false"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to disable sibling elements; <see langword="false"/> to leave
    /// sibling elements unaffected.
    /// </value>
    /// <remarks>
    /// When enabled, sibling elements in the host's parent panel are disabled while the
    /// <see cref="ContentDialog"/> is displayed. This option is disabled by default and
    /// is intended for scenarios where background interaction must be prevented.
    /// </remarks>
    public bool IsDisableSiblingsEnabled
    {
        get => (bool)GetValue(IsDisableSiblingsEnabledProperty);
        set => SetValue(IsDisableSiblingsEnabledProperty, value);
    }

    protected override void OnContentChanged(object? oldContent, object? newContent)
    {
        // Transition: no dialog -> dialog (first open)
        if (oldContent == null && newContent != null)
        {
            _controller.HandleDialogAdded();
            base.OnContentChanged(oldContent, newContent);
            return;
        }

        // Transition: dialog -> no dialog (last close)
        if (oldContent != null && newContent == null)
        {
            base.OnContentChanged(oldContent, newContent);
            _controller.HandleDialogRemoved();
            return;
        }

        // Replacement: oldContent != null && newContent != null
        base.OnContentChanged(oldContent, newContent);
    }

    private static void OnIsDisableSiblingsEnabledChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is ContentDialogHost host)
        {
            host._controller.IsDisableSiblingsEnabled = (bool)e.NewValue;
        }
    }
}

#pragma warning restore IDE0008 // Use explicit type instead of 'var'
