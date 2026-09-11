// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Supplies optional literal address keys; consumers own all page geometry.</summary>
internal static class AddressKeyboardKeys
{
    internal static SoftKeyboardKey Domain(CultureInfo culture) => new(
        SoftKeyboardKeyKind.Text,
        ".com",
        ".com",
        text: ".com",
        alternates: GetDomainAlternates(culture),
        alternateHint: ".net"
    );

    internal static SoftKeyboardKey Period() => new(
        SoftKeyboardKeyKind.Text,
        ".",
        "Period",
        text: ".",
        alternates: ["?"],
        alternateHint: "?"
    );

    /// <summary>
    /// Keeps common domain shortcuts and adds at most one country suffix for the active input language.
    /// </summary>
    private static IEnumerable<string> GetDomainAlternates(CultureInfo culture)
    {
        yield return ".net";
        yield return ".org";
        yield return ".edu";
        yield return ".gov";

        string countryCode;
        try
        {
            // The context follows the input language, not the application's UI or formatting culture.
            // Custom providers may supply a neutral culture; use its default specific region. An
            // invariant or unresolvable culture has no country shortcut, leaving the common ones usable.
            var regionalCulture = culture.IsNeutralCulture
                ? CultureInfo.CreateSpecificCulture(culture.Name)
                : culture;

            countryCode = new RegionInfo(regionalCulture.Name).TwoLetterISORegionName;
        }
        catch (ArgumentException)
        {
            yield break;
        }

        // Macro-regions (such as 419) are not country domains. The UK's domain uses UK rather than GB.
        if (countryCode.Length == 2)
        {
            yield return countryCode == "GB" ? ".uk" : "." + countryCode.ToLowerInvariant();
        }
    }
}
