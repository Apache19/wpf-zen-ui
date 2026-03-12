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
/// During XAML parsing, all property changes are batched via <see cref="ISupportInitialize"/>
/// so that <see cref="ThemeManager.Configure"/> is called only once after all properties are set.
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
///             &lt;ui:FluentTheme
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
///             &lt;ui:FluentTheme
///                 ThemeMode="Dark"
///                 UseSystemAccentColor="True"
///                 AdditionalLightThemeSource="pack://application:,,,/MyApp;component/Themes/Light.xaml"
///                 AdditionalDarkThemeSource="pack://application:,,,/MyApp;component/Themes/Dark.xaml" /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/UserControl.Resources&gt;
/// </code>
/// </example>
public class ThemeManagerOptions : ResourceDictionary, ISupportInitialize
{
    // todo 此可以作为库初始化入口，ThemeManager也可以

    private bool _isInitializing;
    private bool _pendingApply;

    private UiThemeMode _themeMode = UiThemeMode.Default;
    private bool _useSystemAccentColor = true;
    private Uri? _additionalLightThemeSource;
    private Uri? _additionalDarkThemeSource;
    private Uri? _additionalHighContrastLightThemeSource;
    private Uri? _additionalHighContrastDarkThemeSource;

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
    /// Gets or sets an additional resource dictionary URI to merge when the Light theme is active.
    /// </summary>
    public Uri? AdditionalLightThemeSource
    {
        get => _additionalLightThemeSource;
        set
        {
            if (_additionalLightThemeSource != value)
            {
                _additionalLightThemeSource = value;
                RequestApply();
            }
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
            if (_additionalDarkThemeSource != value)
            {
                _additionalDarkThemeSource = value;
                RequestApply();
            }
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
            if (_additionalHighContrastLightThemeSource != value)
            {
                _additionalHighContrastLightThemeSource = value;
                RequestApply();
            }
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
            if (_additionalHighContrastDarkThemeSource != value)
            {
                _additionalHighContrastDarkThemeSource = value;
                RequestApply();
            }
        }
    }

    /// <inheritdoc cref="ISupportInitialize.BeginInit"/>
    void ISupportInitialize.BeginInit()
    {
        _isInitializing = true;
        BeginInit();
    }

    /// <inheritdoc cref="ISupportInitialize.EndInit"/>
    void ISupportInitialize.EndInit()
    {
        EndInit();
        _isInitializing = false;

        if (_pendingApply)
        {
            _pendingApply = false;
            ApplyConfiguration();
        }
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
            _additionalLightThemeSource,
            _additionalDarkThemeSource,
            _additionalHighContrastLightThemeSource,
            _additionalHighContrastDarkThemeSource
        );
    }
}