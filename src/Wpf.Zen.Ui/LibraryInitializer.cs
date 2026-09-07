// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Provides the single entry point for process-wide library initialization.
/// </summary>
/// <remarks>
/// WPF metadata customization and class-handler registration must be completed before an
/// application creates native controls or dispatches popup input. The module initializer forces
/// this type's static constructor to run when the assembly is loaded, making that behavior
/// independent of which library control or theme service an application touches first.
///
/// Existing library entry points may still call <see cref="EnsureInitialized"/> explicitly. Such
/// calls are intentionally harmless and document that the caller depends on library initialization.
/// </remarks>
internal static class LibraryInitializer
{
    static LibraryInitializer()
    {
        TextBlock.InitializeMetadata();
        PopupAlignment.Initialize();
        PopupDismissal.Initialize();
    }

    /// <summary>
    /// Initializes process-wide WPF metadata and input policies when the assembly is loaded, or
    /// ensures that initialization has already completed when called explicitly.
    /// </summary>
    /// <remarks>
    /// The runtime invokes this method as a module initializer. Before entering the method, CLR type
    /// initialization guarantees execution of <see cref="LibraryInitializer"/>'s static constructor.
    ///
    /// The empty body also provides a convenient explicit initialization barrier to existing callers
    /// without repeating either initialization operation.
    /// </remarks>
    [ModuleInitializer]
    [SuppressMessage(
        "Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification =
            "Library-wide WPF metadata and class handlers must be installed before controls are used."
    )]
    public static void EnsureInitialized()
    {
    }
}
