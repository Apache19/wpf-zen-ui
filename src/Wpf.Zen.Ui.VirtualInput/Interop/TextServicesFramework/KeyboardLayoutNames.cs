// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Security;
using Microsoft.Win32;
using Windows.Win32;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Resolves installed layout names without activating a layout or disturbing editor composition.
/// </summary>
internal static class KeyboardLayoutNames
{
    /// <summary>
    /// Resolves the localized layout resource separately from its language. Custom layouts use their
    /// registered Layout Id; an unavailable registration falls back to a distinct handle label.
    /// </summary>
    internal static string GetDisplayName(IntPtr handle, ushort languageId)
    {
        string language;
        try
        {
            language = new CultureInfo(languageId).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            language = $"0x{languageId:X4}";
        }

        var layout = unchecked((uint)handle.ToInt64());
        var device = layout >> 16;

        try
        {
            using var layouts = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts"
            );

            if (layouts is not null)
            {
                foreach (var name in layouts.GetSubKeyNames())
                {
                    if (!uint.TryParse(name, NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                        out var identifier))
                    {
                        continue;
                    }

                    using var entry = layouts.OpenSubKey(name);

                    var matches = identifier == device;

                    if ((device & 0xF000) == 0xF000)
                    {
                        matches = (identifier & 0xFFFF) == languageId
                            && entry?.GetValue("Layout Id") is string id
                            && uint.TryParse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var layoutId)
                            && layoutId == (device & 0x0FFF);
                    }

                    if (!matches || entry is null)
                    {
                        continue;
                    }

                    var localizedName = LoadLocalizedName(entry.GetValue("Layout Display Name") as string);
                    if (localizedName is not null)
                    {
                        return localizedName;
                    }

                    if (entry.GetValue("Layout Text") is string text
                        && !string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }
        catch (Exception exception) when (
            exception is SecurityException or UnauthorizedAccessException or System.IO.IOException
        )
        {
            // Registry access is optional; the handle still identifies the selectable layout.
        }

        return $"{language} ({layout:X8})";
    }

    /// <summary>
    /// Loads the registered indirect string using Windows UI language resource selection.
    /// Returns null when the resource is absent or cannot be resolved, allowing the registered
    /// plain-text name to serve as a fallback.
    /// </summary>
    private static unsafe string? LoadLocalizedName(string? resource)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return null;
        }

        var buffer = new char[1024];

        string name;

        fixed (char* source = resource)
        fixed (char* destination = buffer)
        {
            if (PInvoke.SHLoadIndirectString(source, destination, (uint)buffer.Length, null).Failed)
            {
                return null;
            }

            name = new string(destination);
        }

        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
