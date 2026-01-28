// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal interface IDwmAttributeSetter
{
    public HRESULT Apply(HWND hwnd);
}

internal sealed class DwmAttributeSetter<T> : IDwmAttributeSetter
    where T : unmanaged
{
    private readonly DWMWINDOWATTRIBUTE _attrib;
    private readonly T                  _value;

    public DwmAttributeSetter(DWMWINDOWATTRIBUTE attrib, T value)
    {
        _attrib = attrib;
        _value  = value;
    }

    public unsafe HRESULT Apply(HWND hwnd)
    {
        var v = _value;
        return PInvoke.DwmSetWindowAttribute(hwnd, _attrib, &v, (uint)sizeof(T));
    }
}