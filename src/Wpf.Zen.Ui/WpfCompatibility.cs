// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Provides explicit startup configuration for WPF compatibility behaviors used by the host application.
/// </summary>
/// <remarks>
/// These settings affect WPF throughout the current application domain (.NET Framework) or process
/// (modern .NET), including native and third-party controls. Referencing the library does not opt the
/// host into these settings; the host must call the relevant method at its startup boundary.
/// </remarks>
public static class WpfCompatibility
{
    private const string UseAdornerForTextboxSelectionRenderingSwitchName =
        "Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering";

    /// <summary>
    /// Gets a value indicating whether the host explicitly requests WPF's selection renderer that draws backgrounds beneath text.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when the adorner-selection compatibility switch is explicitly
    /// <see langword="false"/>; otherwise, <see langword="false"/>, including when no switch is configured.
    /// </value>
    /// <remarks>
    /// <para>
    /// Recognizes configuration supplied by <see cref="EnableModernTextSelectionRendering"/>,
    /// AppContext, or the host's runtime configuration. Reading this property does not initialize
    /// WPF text controls or change the switch. It reports the requested startup configuration,
    /// not WPF's cached renderer: the host must configure the switch before WPF text initialization.
    /// </para>
    /// <para>
    /// XAML styles can use this property as an x:Static source for a one-time DataTrigger binding.
    /// Configure the switch before loading those styles; x:Static captures the value when XAML is
    /// loaded, and this property does not publish change notifications for runtime switching.
    /// </para>
    /// </remarks>
    public static bool IsModernTextSelectionRenderingEnabled =>
        AppContext.TryGetSwitch(UseAdornerForTextboxSelectionRenderingSwitchName, out var useAdorner)
        && !useAdorner;

    /// <summary>
    /// Enables selection backgrounds drawn beneath text in WPF TextBox and PasswordBox controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call this method at the beginning of the application's entry point, before creating controls,
    /// initializing themes, loading XAML resources, or otherwise initializing WPF text editing types.
    /// WPF caches the compatibility switch and calculates selection opacity metadata on first use;
    /// calling this method afterwards cannot reliably change the renderer or repair that metadata.
    /// This method configures startup behavior and is not a runtime toggle or a check of the active renderer.
    /// Repeating the call before WPF initialization is harmless.
    /// </para>
    /// <para>
    /// WPF normally uses a translucent selection adorner above the text. This method opts into its
    /// built-in under-text renderer, available since .NET Framework 4.7.2, whose default selection
    /// opacity is 1. Explicit selection opacity values and brush alpha are still respected.
    /// The setting affects all WPF TextBox and PasswordBox controls in the host, not just library
    /// controls. RichTextBox retains its adorner-based selection rendering.
    /// </para>
    /// <para>
    /// Selection foreground customization through SelectionTextBrush additionally requires
    /// .NET Framework 4.8 or modern .NET; .NET Framework 4.7.2 uses the system highlight text brush.
    /// This method does not directly set selection colors or change the host's theme. Styles can
    /// use <see cref="IsModernTextSelectionRenderingEnabled"/> to choose appropriate selection brushes.
    /// </para>
    /// </remarks>
    public static void EnableModernTextSelectionRendering()
    {
        // Use WPF's public compatibility mechanism without touching text-control types here:
        // their static initialization may cache the old switch value before it is changed.
        AppContext.SetSwitch(UseAdornerForTextboxSelectionRenderingSwitchName, false);
    }
}
