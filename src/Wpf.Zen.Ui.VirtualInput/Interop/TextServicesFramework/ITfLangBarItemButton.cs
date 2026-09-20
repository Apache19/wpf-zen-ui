// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Includes the inherited ITfLangBarItem slots before the language-bar button's OnClick slot.
/// </summary>
[ComImport]
[Guid("28C7F1D0-DE25-11D2-AFDD-00105A2799B5")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface ITfLangBarItemButton
{
    [PreserveSig]
    int GetInfo(out TfInputModeItemInfo info);

    [PreserveSig]
    int GetStatus(out uint status);

    /// <summary>
    /// Requests native surface visibility using a Win32 BOOL.
    /// </summary>
    [PreserveSig]
    int Show(int show);

    [PreserveSig]
    int GetTooltipString([MarshalAs(UnmanagedType.BStr)] out string? tooltip);

    /// <summary>
    /// Invokes the native language-bar action using its click kind and screen-space anchor.
    /// </summary>
    [PreserveSig]
    int OnClick(uint click, TfInputModePoint point, in TfInputModeRectangle rectangle);

    [PreserveSig]
    int InitMenu(IntPtr menu);

    [PreserveSig]
    int OnMenuSelect(uint menuItemId);

    /// <summary>
    /// Acquires the native icon used to create the input-mode button image.
    /// </summary>
    [PreserveSig]
    int GetIcon(out IntPtr icon);

    /// <summary>
    /// Reads the item's textual presentation when provided by the active service.
    /// </summary>
    [PreserveSig]
    int GetText([MarshalAs(UnmanagedType.BStr)] out string? text);
}

/// <summary>
/// Preserves the fixed Unicode description buffer and service identity in TF_LANGBARITEMINFO.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TfInputModeItemInfo
{
    internal Guid ServiceId;
    internal Guid ItemId;
    internal uint Style;
    internal uint Sort;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    internal string Description;
}

/// <summary>
/// Represents the by-value Win32 POINT used by the language-bar click contract on x86 and x64.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TfInputModePoint
{
    internal int X;
    internal int Y;
}

/// <summary>
/// Represents the Win32 RECT passed as the optional menu anchor for a language-bar click.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TfInputModeRectangle
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}
