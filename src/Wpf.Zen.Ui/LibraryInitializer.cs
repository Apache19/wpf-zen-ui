// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Provides a centralized entry point to ensure that the library-level initialization logic
/// is executed exactly once.
/// </summary>
/// <remarks>
/// Some controls in the library require metadata overrides or other initialization logic
/// to run before the control is used for the first time. This type acts as a lightweight
/// trigger to guarantee that such initialization happens early and only once.
///
/// Any frequently used type in the library may call <see cref="EnsureInitialized"/> from its
/// static constructor to ensure that the initialization logic has been executed.
/// </remarks>
internal static class LibraryInitializer
{
    static LibraryInitializer()
    {
        TextBlock.InitializeMetadata();
    }

    /// <summary>
    /// Ensures that the library initialization logic has been executed.
    /// </summary>
    /// <remarks>
    /// Calling this method forces the runtime to trigger the static constructor
    /// of <see cref="LibraryInitializer"/> if it has not already run.
    ///
    /// The method intentionally contains no implementation. Its sole purpose
    /// is to provide a convenient and explicit way for other types to guarantee
    /// that the library initialization has occurred.
    /// </remarks>
    public static void EnsureInitialized()
    {
    }
}