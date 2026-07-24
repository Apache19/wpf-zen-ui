// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Appearance;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui;

/// <summary>
/// A resource dictionary that configures the WPF UI theme system declaratively in XAML.
/// Place this dictionary in <c>Application.Resources</c> to set the global theme mode,
/// accent color behavior, and additional theme resource URIs.
/// In non-WPF environments, such as WinForms projects, it can be merged into the first
/// XAML content like a UserControl.
/// </summary>
/// <remarks>
/// <para>
/// During XAML parsing, property changes are batched through <see cref="ISupportInitialize"/>
/// and applied once after the dictionary is fully initialized.
/// </para>
/// <para>
/// At runtime, changing any property after initialization triggers an immediate theme refresh.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;Application.Resources&gt;
///     &lt;ResourceDictionary&gt;
///         &lt;ResourceDictionary.MergedDictionaries&gt;
///             &lt;ui:ThemeManagerOptions
///                 ThemeMode="Dark"
///                 UseSystemAccentColor="True"
///                 AdditionalLightThemeSource="pack://application:,,,/MyApp;component/Themes/Light.xaml"
///                 AdditionalDarkThemeSource="pack://application:,,,/MyApp;component/Themes/Dark.xaml" /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/Application.Resources&gt;
/// </code>
/// </example>
/// <example>
/// <code lang="xml">
/// &lt;UserControl.Resources&gt;
///     &lt;ResourceDictionary&gt;
///         &lt;ResourceDictionary.MergedDictionaries&gt;
///             &lt;ui:ThemeManagerOptions
///                 ThemeMode="Dark"
///                 UseSystemAccentColor="True"
///                 AdditionalLightThemeSource="pack://application:,,,/MyApp;component/Themes/Light.xaml"
///                 AdditionalDarkThemeSource="pack://application:,,,/MyApp;component/Themes/Dark.xaml" /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/UserControl.Resources&gt;
/// </code>
/// </example>
/// <example>
/// <code lang="csharp">
///
/// // Programmatic usage from C# - configure ThemeManager via ThemeManagerOptions
/// var options = new ThemeManagerOptions();
///
/// options.BeginInit(); // Request deferred configuration
///
/// options.ThemeMode = UiThemeMode.Dark;
/// options.UseSystemAccentColor = true;
/// options.AdditionalLightThemeSource = new Uri("pack://application:,,,/MyApp;component/Themes/Light.xaml");
/// options.AdditionalDarkThemeSource = new Uri("pack://application:,,,/MyApp;component/Themes/Dark.xaml");
///
/// // To explicitly clear a previously set URI, set the property to null:
/// // options.AdditionalLightThemeSource = null;
///
/// options.EndInit(); // Apply theme configuration
///
/// </code>
/// </example>
public class ThemeManagerOptions : ResourceDictionary, ISupportInitialize
{
    private bool _isInitializing;
    private bool _pendingApply;

    private UiThemeMode _themeMode = UiThemeMode.Default;
    private bool _useSystemAccentColor = true;
    private bool _enableNativeControlStyling = true;
    private Uri? _additionalLightThemeSource;
    private Uri? _additionalDarkThemeSource;
    private Uri? _additionalHighContrastLightThemeSource;
    private Uri? _additionalHighContrastDarkThemeSource;

    // Flags to track whether a URI property was explicitly set (even to null) from XAML
    private bool _aLightChanged;
    private bool _aDarkChanged;
    private bool _aHcLightChanged;
    private bool _aHcDarkChanged;

    /// <summary>
    /// Gets or sets the global theme mode for the application.
    /// </summary>
    public UiThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (_themeMode != value)
            {
                _themeMode = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to use the system accent color.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool UseSystemAccentColor
    {
        get => _useSystemAccentColor;
        set
        {
            if (_useSystemAccentColor != value)
            {
                _useSystemAccentColor = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to merge implicit styles
    /// for native WPF controls so they adopt the Fluent UI theme.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool EnableNativeControlStyling
    {
        get => _enableNativeControlStyling;
        set
        {
            if (_enableNativeControlStyling != value)
            {
                _enableNativeControlStyling = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when the Light theme is active.
    /// </summary>
    public Uri? AdditionalLightThemeSource
    {
        get => _additionalLightThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aLightChanged = true;
            _additionalLightThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when the Dark theme is active.
    /// </summary>
    public Uri? AdditionalDarkThemeSource
    {
        get => _additionalDarkThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aDarkChanged = true;
            _additionalDarkThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when a light high contrast theme is active.
    /// </summary>
    public Uri? AdditionalHighContrastLightThemeSource
    {
        get => _additionalHighContrastLightThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aHcLightChanged = true;
            _additionalHighContrastLightThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when a dark high contrast theme is active.
    /// </summary>
    public Uri? AdditionalHighContrastDarkThemeSource
    {
        get => _additionalHighContrastDarkThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aHcDarkChanged = true;
            _additionalHighContrastDarkThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Begins a configuration transaction so multiple property changes are applied in one refresh.
    /// </summary>
    /// <remarks>
    /// This member intentionally hides <see cref="ResourceDictionary.BeginInit"/>. Calling the
    /// inherited method directly would start WPF resource initialization without enabling the
    /// configuration batching promised by this type.
    /// </remarks>
    public new void BeginInit()
    {
        base.BeginInit();
        _isInitializing = true;
    }

    /// <summary>
    /// Ends the configuration transaction and applies all pending changes once.
    /// </summary>
    public new void EndInit()
    {
        base.EndInit();
        _isInitializing = false;

        if (_pendingApply)
        {
            _pendingApply = false;
            ApplyConfiguration();
        }
    }

    /// <inheritdoc cref="ISupportInitialize.BeginInit"/>
    void ISupportInitialize.BeginInit()
    {
        BeginInit();
    }

    /// <inheritdoc cref="ISupportInitialize.EndInit"/>
    void ISupportInitialize.EndInit()
    {
        EndInit();
    }

    private void RequestApply()
    {
        if (_isInitializing)
        {
            _pendingApply = true;
        }
        else
        {
            ApplyConfiguration();
        }
    }

    private void ApplyConfiguration()
    {
        ThemeManager.Configure(
            _themeMode,
            _useSystemAccentColor,
            _enableNativeControlStyling,
            _aLightChanged ? _additionalLightThemeSource : ThemeManager.AdditionalLightThemeUri,
            _aDarkChanged ? _additionalDarkThemeSource : ThemeManager.AdditionalDarkThemeUri,
            _aHcLightChanged ? _additionalHighContrastLightThemeSource : ThemeManager.AdditionalHighContrastLightThemeUri,
            _aHcDarkChanged ? _additionalHighContrastDarkThemeSource : ThemeManager.AdditionalHighContrastDarkThemeUri
        );

        // Reset explicit-set flags after applying so subsequent merges without that attribute
        // don't unintentionally clear values.
        _aLightChanged = _aDarkChanged = _aHcLightChanged = _aHcDarkChanged = false;
    }

    static ThemeManagerOptions()
    {
        LibraryInitializer.EnsureInitialized();
    }
}