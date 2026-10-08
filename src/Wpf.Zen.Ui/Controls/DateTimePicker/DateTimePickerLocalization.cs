// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Resources;
using System.Windows.Data;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Converts the control's Language and a resource key to localized text. Bindings reevaluate when
/// Language changes.
/// </summary>
internal sealed class DateTimePickerLocalization : IValueConverter
{
    private static readonly ResourceManager Resources = new(
        "Wpf.Zen.Ui.Controls.DateTimePicker.DateTimePickerStrings",
        typeof(DateTimePickerLocalization).Assembly
    );

    /// <summary>
    /// Looks up a resource in the requested culture, falling back to neutral English resources when
    /// a translation is missing.
    /// </summary>
    internal static string GetString(string key, CultureInfo culture)
    {
        return Resources.GetString(key, culture) ?? key;
    }

    /// <summary>
    /// Selects translations using the bound Language, or the current UI culture when no valid
    /// language is available.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var language        = value as XmlLanguage;
        var resourceCulture = GetCulture(language, CultureInfo.CurrentUICulture);
        var key             = (string)parameter;
        return GetString(key, resourceCulture);
    }

    /// <summary>
    /// A nonempty language tag overrides the caller's culture. Empty or unresolved tags use the
    /// fallback culture.
    /// </summary>
    internal static CultureInfo GetCulture(XmlLanguage? language, CultureInfo fallback)
    {
        if (language is null || language.IetfLanguageTag.Length == 0)
        {
            return fallback;
        }

        try
        {
            return language.GetSpecificCulture();
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Localized labels are read-only; conversion does not write back to Language.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}