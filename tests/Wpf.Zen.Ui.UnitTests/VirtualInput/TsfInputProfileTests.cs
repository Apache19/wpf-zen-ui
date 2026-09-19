// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32.UI.TextServices;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Checks selectable profile kinds and exact layout identity independently of installed input
/// methods.
/// </summary>
public class TsfInputProfileTests
{
    /// <summary>
    /// The platform profile source does not interpret service-defined compartment values.
    /// </summary>
    [Fact]
    public void WindowsBackend_PublishesNoGenericInputMode()
    {
        Assert.Null(typeof(ISoftKeyboardInputMethodSession).GetMethod("GetInputMode"));
        Assert.Null(typeof(ISoftKeyboardInputMethodSession).GetMethod("TrySwitchInputMode"));

    }

    /// <summary>
    /// An enabled TSF registration cannot promote an unloaded layout into the switcher.
    /// Loading one layout must not admit another layout of the same language.
    /// </summary>
    [Fact]
    public void RegisteredLayouts_RequireExactLoadedHandles()
    {
        var loaded = new System.Collections.Generic.HashSet<IntPtr>();
        var us = Layout(0x04090409);
        var dvorak = Layout(unchecked((int)0xF0020409));
        Assert.False(TsfInputMethodService.IsSelectableProfile(us, loaded));
        loaded.Add(new IntPtr(0x04090409));
        Assert.True(TsfInputMethodService.IsSelectableProfile(us, loaded));
        Assert.False(TsfInputMethodService.IsSelectableProfile(dvorak, loaded));
        loaded.Add(new IntPtr(unchecked((int)0xF0020409)));
        Assert.True(TsfInputMethodService.IsSelectableProfile(dvorak, loaded));
    }

    /// <summary>
    /// Disabled and substituted layouts are not separate input choices.
    /// </summary>
    [Theory]
    [InlineData(0U, false)]
    [InlineData(2U, true)]
    [InlineData(3U, true)]
    [InlineData(6U, false)]
    public void KeyboardLayout_FiltersNativeFlags(uint flags, bool expected)
    {
        var layout = Layout(0x04090409);
        layout.dwFlags = flags;
        Assert.Equal(expected, TsfInputMethodService.IsSelectableProfile(layout, new System.Collections.Generic.HashSet<IntPtr> { new IntPtr(0x04090409) }));
    }

    /// <summary>
    /// US and Dvorak layouts share a language but must remain distinct selections.
    /// </summary>
    [Fact]
    public void ActiveLayout_UsesHandleInsteadOfLanguageOrEmptyGuids()
    {
        var first = Layout(0x04090409);
        var second = Layout(unchecked((int)0xF0020409));
        Assert.True(TsfInputMethodService.IsSameProfile(first, first));
        Assert.False(TsfInputMethodService.IsSameProfile(first, second));
        second.dwProfileType = 1;
        second.hkl = first.hkl;
        Assert.False(TsfInputMethodService.IsSameProfile(first, second));
    }

    /// <summary>
    /// Profiles outside the keyboard category must not enter the input switcher.
    /// </summary>
    [Fact]
    public void Tip_RequiresKeyboardCategoryAndEnabledIdentity()
    {
        var tip = new TF_INPUTPROCESSORPROFILE
        {
            dwProfileType = 1,
            dwFlags = 2,
            langid = 0x0409,
            clsid = Guid.NewGuid(),
            guidProfile = Guid.NewGuid(),
        };
        Assert.False(TsfInputMethodService.IsSelectableProfile(tip, new System.Collections.Generic.HashSet<IntPtr>()));
        tip.catid = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");
        Assert.True(TsfInputMethodService.IsSelectableProfile(tip, new System.Collections.Generic.HashSet<IntPtr>()));
        tip.dwFlags = 0;
        Assert.False(TsfInputMethodService.IsSelectableProfile(tip, new System.Collections.Generic.HashSet<IntPtr>()));
    }

    /// <summary>
    /// Public profile descriptions carry one native identity kind at a time.
    /// </summary>
    [Fact]
    public void InputMethod_PreservesExactLayoutAndRejectsMixedIdentity()
    {
        var handle = new IntPtr(0x04090409);
        var method = new SoftKeyboardInputMethod(Guid.Empty, Guid.Empty, 0x0409, "US", true,
            keyboardLayout: handle);
        Assert.Equal(handle, method.KeyboardLayout);
        Assert.Throws<ArgumentException>(() => new SoftKeyboardInputMethod(
            Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Invalid", false, keyboardLayout: handle));
    }

    private static TF_INPUTPROCESSORPROFILE Layout(int handle)
    {
        return new TF_INPUTPROCESSORPROFILE
        {
            dwProfileType = 2,
            dwFlags = 2,
            langid = 0x0409,
            hkl = new(new IntPtr(handle)),
        };
    }
}
