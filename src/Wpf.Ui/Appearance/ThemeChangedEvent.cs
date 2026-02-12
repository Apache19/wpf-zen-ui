// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

/// <summary>
/// Represents the method that will handle the application theme changed event.
/// </summary>
/// <param name="sender">The source of the event, typically the object that triggered the theme change.</param>
/// <param name="e">An object that contains theme change related event data.</param>
/// <remarks>
/// This event is raised when the application theme (such as light/dark mode) changes.
/// Subscribe to this event to perform UI updates when the theme changes.
/// </remarks>
public delegate void ThemeChangedEvent(object? sender, ThemeChangedEventArgs e);
