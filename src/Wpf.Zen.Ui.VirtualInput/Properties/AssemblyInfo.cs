// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Markup;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: InternalsVisibleTo("Wpf.Zen.Ui.UnitTests")]

[assembly: XmlnsPrefix("http://schemas.thezendev.com/wpf/2027/virtual-input", "keyboard")]
[assembly: XmlnsDefinition("http://schemas.thezendev.com/wpf/2027/virtual-input", "Wpf.Zen.Ui.VirtualInput.Controls")]
[assembly: XmlnsDefinition("http://schemas.thezendev.com/wpf/2027/virtual-input", "Wpf.Zen.Ui.VirtualInput.Input")]
[assembly: XmlnsDefinition("http://schemas.thezendev.com/wpf/2027/virtual-input", "Wpf.Zen.Ui.VirtualInput.Layouts")]
[assembly: XmlnsDefinition("http://schemas.thezendev.com/wpf/2027/virtual-input", "Wpf.Zen.Ui.VirtualInput.Layouts.Providers")]
