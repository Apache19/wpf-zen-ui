// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;
using static Wpf.Ui.Appearance.UISettingsRCW;

namespace Wpf.Ui.Appearance;

/// <summary>
/// Allows updating the accents used by controls in the application by swapping dynamic resources.
/// </summary>
/// <example>
/// <code lang="csharp">
/// ApplicationAccentColorManager.Apply(
///     Color.FromArgb(0xFF, 0xEE, 0x00, 0xBB),
///     ApplicationTheme.Dark,
///     false
/// );
/// </code>
/// <code lang="csharp">
/// ApplicationAccentColorManager.Apply(
///     ApplicationAccentColorManager.GetColorizationColor(),
///     ApplicationTheme.Dark,
///     false
/// );
/// </code>
/// </example>
public static class ApplicationAccentColorManager
{
    /// <summary>
    /// The maximum value of the background HSV brightness after which the text on the accent will be turned dark.
    /// </summary>
    private const double BackgroundBrightnessThresholdValue = 80d;

    private static readonly IUISettings3? _uisettings;
    private static readonly bool          _isSupported;

    static ApplicationAccentColorManager()
    {
        try
        {
            _uisettings  = GetWinRTInstance() as IUISettings3;
            _isSupported = _uisettings != null;
        }
        catch (COMException)
        {
            // We don't want to throw any exceptions here.
            // If we can't get the instance, we will use the fallback accent color.
        }
    }

    /// <summary>
    /// Gets the SystemAccentColor.
    /// </summary>
    public static Color SystemAccentColor
    {
        get
        {
            object? resource = Application.Current.Resources["SystemAccentColor"];

            if (resource is Color color)
            {
                return color;
            }

            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Gets the <see cref="Brush"/> of the SystemAccentColor.
    /// </summary>
    public static Brush SystemAccentColorBrush => new SolidColorBrush(SystemAccentColor);

    /// <summary>
    /// Gets the SystemAccentColorPrimary.
    /// </summary>
    public static Color PrimaryAccentColor
    {
        get
        {
            object? resource = Application.Current.Resources["SystemAccentColorPrimary"];

            if (resource is Color color)
            {
                return color;
            }

            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Gets the <see cref="Brush"/> of the SystemAccentColorPrimary.
    /// </summary>
    public static Brush PrimaryAccentColorBrush => new SolidColorBrush(PrimaryAccentColor);

    /// <summary>
    /// Gets the SystemAccentColorSecondary.
    /// </summary>
    public static Color SecondaryAccentColor
    {
        get
        {
            object? resource = Application.Current.Resources["SystemAccentColorSecondary"];

            if (resource is Color color)
            {
                return color;
            }

            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Gets the <see cref="Brush"/> of the SystemAccentColorSecondary.
    /// </summary>
    public static Brush SecondaryAccentColorBrush => new SolidColorBrush(SecondaryAccentColor);

    /// <summary>
    /// Gets the SystemAccentColorTertiary.
    /// </summary>
    public static Color TertiaryAccentColor
    {
        get
        {
            object? resource = Application.Current.Resources["SystemAccentColorTertiary"];

            if (resource is Color color)
            {
                return color;
            }

            return Colors.Transparent;
        }
    }

    /// <summary>
    /// Gets the <see cref="Brush"/> of the SystemAccentColorTertiary.
    /// </summary>
    public static Brush TertiaryAccentColorBrush => new SolidColorBrush(TertiaryAccentColor);

    /// <summary>
    /// Gets a value indicating whether the user has enabled accent color on title bars and window borders in Windows settings.
    /// </summary>
    public static bool IsAccentColorOnTitleBarsEnabled =>
        UnsafeNativeMethods.IsAccentColorOnTitleBarsEnabled();

    /// <summary>
    /// Changes the color accents of the application based on the color entered.
    /// </summary>
    /// <param name="systemAccent">Primary accent color.</param>
    /// <param name="applicationTheme">If <see cref="ApplicationTheme.Dark"/>, the colors will be different.</param>
    /// <param name="systemGlassColor">If the color is taken from the Glass Color System, its brightness will be increased with the help of the operations on HSV space.</param>
    /// <param name="systemAccentColor">If the color is the system accent color.</param>
    public static void Apply(
        Color systemAccent,
        ThemeMode applicationTheme = ThemeMode.Light,
        bool systemGlassColor = false,
        bool systemAccentColor = false
    )
    {
        if (systemGlassColor)
        {
            // WindowGlassColor is little darker than accent color
            systemAccent = systemAccent.UpdateBrightness(6f);
        }

        Color primaryAccent;
        Color secondaryAccent;
        Color tertiaryAccent;

        if (applicationTheme == ThemeMode.Dark)
        {
            primaryAccent   = GetColor(UIColorType.AccentLight1, 17, -30f);
            secondaryAccent = GetColor(UIColorType.AccentLight2, 17, -45f);
            tertiaryAccent  = GetColor(UIColorType.AccentLight3, 17, -65f);
        }
        else
        {
            primaryAccent   = GetColor(UIColorType.AccentDark1, -10);
            secondaryAccent = GetColor(UIColorType.AccentDark2, -25);
            tertiaryAccent  = GetColor(UIColorType.AccentDark3, -40);
        }

        UpdateColorResources(applicationTheme, systemAccent, primaryAccent, secondaryAccent, tertiaryAccent);

        Color GetColor(UIColorType colorType, float brightnessFactor, float saturationFactor = 0.0f)
        {
            if (systemAccentColor && GetUiColor(colorType) is { } color)
            {
                return color;
            }

            return systemAccent.Update(brightnessFactor, saturationFactor);
        }
    }

    /// <summary>
    /// Changes the color accents of the application based on the entered colors.
    /// </summary>
    /// <param name="systemAccent">Primary color.</param>
    /// <param name="primaryAccent">Alternative light or dark color.</param>
    /// <param name="secondaryAccent">Second alternative light or dark color (most used).</param>
    /// <param name="tertiaryAccent">Third alternative light or dark color.</param>
    public static void Apply(
        Color systemAccent,
        Color primaryAccent,
        Color secondaryAccent,
        Color tertiaryAccent
    )
    {
        UpdateColorResources(
            ThemeManager.ThemeMode,
            systemAccent,
            primaryAccent,
            secondaryAccent,
            tertiaryAccent
        );
    }

    /// <summary>
    /// Applies system accent color to the application.
    /// </summary>
    public static void ApplySystemAccent()
    {
        Apply(GetColorizationColor(), ThemeManager.ThemeMode, systemAccentColor: true);
    }

    /// <summary>
    /// Gets current Desktop Window Manager colorization color.
    /// <para>It should be the color defined in the system Personalization.</para>
    /// </summary>
    public static Color GetColorizationColor()
    {
        if (GetUiColor(UIColorType.Accent) is { } accentColor)
        {
            return accentColor;
        }

        return UnsafeNativeMethods.GetAccentColor();
    }

    /// <summary>
    /// Updates application resources.
    /// </summary>
    private static void UpdateColorResources(
        ThemeMode applicationTheme,
        Color systemAccent,
        Color primaryAccent,
        Color secondaryAccent,
        Color tertiaryAccent)
    {
        // Debug.WriteLine($"INFO | SystemAccentColor: {systemAccent}", "Wpf.Ui.Accent");
        // Debug.WriteLine($"INFO | SystemAccentColorPrimary: {primaryAccent}", "Wpf.Ui.Accent");
        // Debug.WriteLine($"INFO | SystemAccentColorSecondary: {secondaryAccent}", "Wpf.Ui.Accent");
        // Debug.WriteLine($"INFO | SystemAccentColorTertiary: {tertiaryAccent}", "Wpf.Ui.Accent");

        if (secondaryAccent.GetBrightness() > BackgroundBrightnessThresholdValue)
        {
            // Debug.WriteLine("INFO | Text on accent is DARK", "Wpf.Ui.Accent");
            Application.Current.Resources["TextOnAccentFillColorPrimary"]      = Color.FromArgb(0xFF, 0x00, 0x00, 0x00);
            Application.Current.Resources["TextOnAccentFillColorSecondary"]    = Color.FromArgb(0x80, 0x00, 0x00, 0x00);
            Application.Current.Resources["TextOnAccentFillColorDisabled"]     = Color.FromArgb(0x77, 0x00, 0x00, 0x00);
            Application.Current.Resources["TextOnAccentFillColorSelectedText"] = Color.FromArgb(0x00, 0x00, 0x00, 0x00);
            Application.Current.Resources["AccentTextFillColorDisabled"]       = Color.FromArgb(0x5D, 0x00, 0x00, 0x00);
        }
        else
        {
            // Debug.WriteLine("INFO | Text on accent is LIGHT", "Wpf.Ui.Accent");
            Application.Current.Resources["TextOnAccentFillColorPrimary"]      = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            Application.Current.Resources["TextOnAccentFillColorSecondary"]    = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);
            Application.Current.Resources["TextOnAccentFillColorDisabled"]     = Color.FromArgb(0x87, 0xFF, 0xFF, 0xFF);
            Application.Current.Resources["TextOnAccentFillColorSelectedText"] = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);
            Application.Current.Resources["AccentTextFillColorDisabled"]       = Color.FromArgb(0x5D, 0xFF, 0xFF, 0xFF);
        }

        Application.Current.Resources["SystemAccentColor"]          = systemAccent;
        Application.Current.Resources["SystemAccentColorPrimary"]   = primaryAccent;
        Application.Current.Resources["SystemAccentColorSecondary"] = secondaryAccent;
        Application.Current.Resources["SystemAccentColorTertiary"]  = tertiaryAccent;

        Application.Current.Resources["SystemAccentColorBrush"]        = systemAccent.ToBrush();
        Application.Current.Resources["SystemFillColorAttentionBrush"] = secondaryAccent.ToBrush();

        Application.Current.Resources["AccentTextFillColorPrimaryBrush"]   = secondaryAccent.ToBrush();
        Application.Current.Resources["AccentTextFillColorSecondaryBrush"] = tertiaryAccent.ToBrush();
        Application.Current.Resources["AccentTextFillColorTertiaryBrush"]  = primaryAccent.ToBrush();

        Application.Current.Resources["AccentFillColorSelectedTextBackgroundBrush"] =
            systemAccent.ToBrush();

        var themeAccent = applicationTheme == ThemeMode.Dark ? secondaryAccent : primaryAccent;
        Application.Current.Resources["AccentFillColorDefault"]      = themeAccent;
        Application.Current.Resources["AccentFillColorDefaultBrush"] = themeAccent.ToBrush();
        Application.Current.Resources["AccentFillColorSecondary"] = Color.FromArgb(
            229,
            themeAccent.R,
            themeAccent.G,
            themeAccent.B
        ); // 229 = 0.9 * 255
        Application.Current.Resources["AccentFillColorSecondaryBrush"] = themeAccent.ToBrush(0.9);
        Application.Current.Resources["AccentFillColorTertiary"] = Color.FromArgb(
            204,
            themeAccent.R,
            themeAccent.G,
            themeAccent.B
        ); // 204 = 0.8 * 255
            Application.Current.Resources["AccentFillColorTertiaryBrush"] = themeAccent.ToBrush(0.8);
    }

    /// <summary>
    /// Gets the color of the UI.
    /// </summary>
    /// <param name="colorType">Type of the color.</param>
    private static Color? GetUiColor(UIColorType colorType)
    {
        if (_isSupported)
        {
            try
            {
                UIColor uiColor = _uisettings!.GetColorValue(colorType);
                return Color.FromArgb(uiColor.A, uiColor.R, uiColor.G, uiColor.B);
            }
            catch
            {
                // We don't want to throw any exceptions here.
                // If we can't get the instance, we can fallback to another method.
            }
        }

        return null;
    }

    /// <summary>
    ///   Gets the WinRT instance of UISettings.
    /// </summary>
    private static object? GetWinRTInstance()
    {
        if (!Utilities.IsWin_10_Or_Greater)
        {
            return null;
        }

        object? winRtInstance;

        try
        {
            winRtInstance = GetUISettingsInstance();
        }
        catch (Exception e) when (e is TypeLoadException or FileNotFoundException)
        {
            winRtInstance = null;
        }

        return winRtInstance;
    }
}