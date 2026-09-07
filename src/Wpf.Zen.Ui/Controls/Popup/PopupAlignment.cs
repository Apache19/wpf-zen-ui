// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Makes the standard alignment properties participate in target-relative Popup, ContextMenu and Flyout placement.
/// </summary>
/// <remarks>
/// WPF inherits <see cref="FrameworkElement.HorizontalAlignment"/> and
/// <see cref="FrameworkElement.VerticalAlignment"/> onto popup surfaces but its placement engine does not read them.
/// This policy aligns the popup on the axis perpendicular to its requested side: horizontal alignment applies to Top
/// and Bottom, vertical alignment applies to Left and Right, and both apply to Relative and Center. Stretch preserves
/// WPF's native placement on that axis. Point, mouse, absolute, and custom modes retain their native semantics because
/// they do not describe an area of the placement target that can be aligned.
///
/// Alignment is expressed by adjusting the effective placement rectangle. HorizontalOffset and VerticalOffset keep
/// their authored values and WPF applies them after the aligned anchor has been calculated. Explicit placement
/// rectangles, dynamic target and popup sizes, and PopupPlacementGap are recomputed through the same coordinator.
///
/// WPF exposes no class-wide public notification for changes to these inherited dependency properties. The policy
/// therefore appends a type-filtered callback to FrameworkElement's existing metadata by reflection. It deliberately
/// does not call <see cref="DependencyProperty.OverrideMetadata(Type, PropertyMetadata)"/> for Popup, leaving that
/// owner-type metadata slot available to applications and Popup subclasses.
/// </remarks>
internal static class PopupAlignment
{
    private static readonly FieldInfo? PropertyChangedCallbackField = typeof(PropertyMetadata).GetField(
        "_propertyChangedCallback",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    static PopupAlignment()
    {
        AppendAlignmentChangedCallback(FrameworkElement.HorizontalAlignmentProperty);
        AppendAlignmentChangedCallback(FrameworkElement.VerticalAlignmentProperty);

        EventManager.RegisterClassHandler(
            typeof(Popup),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(OnElementLoaded),
            handledEventsToo: true
        );

        EventManager.RegisterClassHandler(
            typeof(ContextMenu),
            ContextMenu.OpenedEvent,
            new RoutedEventHandler(OnContextMenuOpened),
            handledEventsToo: true
        );

        EventManager.RegisterClassHandler(
            typeof(Flyout),
            Flyout.OpenedEvent,
            new RoutedEventHandler(OnFlyoutOpened),
            handledEventsToo: true
        );
    }

    /// <summary>
    /// Ensures alignment callbacks and lifecycle fallback handlers are installed before popup surfaces are used.
    /// </summary>
    internal static void Initialize()
    {
    }

    private static void AppendAlignmentChangedCallback(DependencyProperty property)
    {
        if (PropertyChangedCallbackField is null)
        {
            // The Loaded and ContextMenu.Opened class handlers remain as a safe partial fallback if a future WPF
            // implementation renames this field. In that case a standalone Popup must be loaded through a tree before
            // alignment can be observed, but library initialization and normal WPF placement continue without failure.
            return;
        }

        // Popup normally has no distinct metadata for these properties, so GetMetadata(typeof(Popup)) returns the
        // FrameworkElement instance. Mutating that shared instance with a Popup type guard avoids both unsafe
        // OverrideMetadata timing and taking Popup's one metadata slot away from an application or derived control.
        // WPF merges this base callback into metadata overridden later, preserving application callbacks as well.
        var frameworkMetadata = property.GetMetadata(typeof(FrameworkElement));
        TryAppendAlignmentChangedCallback(frameworkMetadata);

        // An application may have installed Popup or ContextMenu metadata before loading this assembly. That metadata
        // already contains a snapshot of the former base callback, so update each distinct existing instance as well.
        // No new metadata entry is created, and the application's callback remains first in the invocation list.
        var popupMetadata = property.GetMetadata(typeof(Popup));
        if (!ReferenceEquals(popupMetadata, frameworkMetadata))
        {
            TryAppendAlignmentChangedCallback(popupMetadata);
        }

        var contextMenuMetadata = property.GetMetadata(typeof(ContextMenu));
        if (!ReferenceEquals(contextMenuMetadata, frameworkMetadata)
            && !ReferenceEquals(contextMenuMetadata, popupMetadata))
        {
            TryAppendAlignmentChangedCallback(contextMenuMetadata);
        }

        var flyoutMetadata = property.GetMetadata(typeof(Flyout));
        if (!ReferenceEquals(flyoutMetadata, frameworkMetadata)
            && !ReferenceEquals(flyoutMetadata, popupMetadata)
            && !ReferenceEquals(flyoutMetadata, contextMenuMetadata))
        {
            TryAppendAlignmentChangedCallback(flyoutMetadata);
        }
    }

    private static void TryAppendAlignmentChangedCallback(PropertyMetadata metadata)
    {
        try
        {
            var originalCallback = (PropertyChangedCallback?)PropertyChangedCallbackField!.GetValue(metadata);
            PropertyChangedCallbackField.SetValue(metadata, originalCallback + OnAlignmentChanged);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or MemberAccessException
                or TargetException
                or System.Security.SecurityException)
        {
            // Metadata is sealed, so the public setter cannot be used. If reflection is denied or WPF changes the
            // field contract, leave this metadata intact; the lifecycle handlers below still provide a safe fallback.
        }
    }

    private static void OnAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
    {
        if (d is not Popup and not ContextMenu and not Flyout)
        {
            return;
        }

        var horizontalAlignment = (HorizontalAlignment)d.GetValue(
            FrameworkElement.HorizontalAlignmentProperty
        );
        var verticalAlignment = (VerticalAlignment)d.GetValue(
            FrameworkElement.VerticalAlignmentProperty
        );

        PopupPlacementGap.SetAlignmentEnabled(
            d,
            horizontalAlignment != HorizontalAlignment.Stretch
                || verticalAlignment != VerticalAlignment.Stretch
        );
    }

    private static void OnElementLoaded(object sender, RoutedEventArgs e)
    {
        // This covers Popups that participate in the main tree and also provides a reflection-failure fallback.
        // Standalone Popups are discovered by the alignment callback before opening when reflection is available.
        OnAlignmentChanged((Popup)sender, default);
    }

    private static void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        // ContextMenuService finishes coercing its target and placement values while opening. Attach at that point,
        // then keep observing layout so alignment changes made while the menu is open are applied immediately.
        OnAlignmentChanged((ContextMenu)sender, default);
    }

    private static void OnFlyoutOpened(object sender, RoutedEventArgs e)
    {
        // Flyout raises Opened after its private Popup has acquired the final target and measured the surface. Reapply
        // alignment here both as a reflection-failure fallback and to synchronize that final opening geometry.
        OnAlignmentChanged((Flyout)sender, default);
    }
}
