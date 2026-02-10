// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Interop;
using Wpf.Ui.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// FluentWindow Win10 partial
/// </summary>
public partial class FluentWindow
{
    [DllImport("dwmapi.dll", EntryPoint = "#127", PreserveSig = false)]
    private static extern void DwmGetColorizationParameters(out DWM_COLORIZATION_PARAMS parameters);

    private struct DWM_COLORIZATION_PARAMS
    {
        public COLORREF clrColor;
        public COLORREF clrAfterGlow;
        public uint     nIntensity;
        public uint     clrAfterGlowBalance;
        public uint     clrBlurBalance;
        public uint     clrGlassReflectionIntensity;
        public bool     fOpaque;
    }

    private void OnSourceInitialized_Win10(HwndSource hwndSource)
    {
    }
}