// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui;

/// <summary>
/// Provides a that contains implicit styles
/// for standard WPF controls, allowing them to adopt the WPF UI
/// Fluent appearance./// </summary>
/// <remarks>
/// This dictionary only includes style wrappers for standard WPF controls
/// (styles without x). When merged into an application's resources,
/// the default appearance of built-in WPF controls will be replaced with
/// the WPF UI Fluent styles.
/// </remarks>
/// <example>
/// <code lang="xml">
/// <Application xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
///     <Application.Resources>
///         <ResourceDictionary>
///             <ResourceDictionary.MergedDictionaries>
///                 <ui:StandardControlDictionary />
///             </ResourceDictionary.MergedDictionaries>
///         </ResourceDictionary>
///     </Application.Resources>
/// </Application>
/// </code>
/// </example>
[Localizability(LocalizationCategory.Ignore)]
[Ambient]
[UsableDuringInitialization(true)]
public class StandardControlDictionary : ResourceDictionary
{
    static StandardControlDictionary()
    {
        LibraryInitializer.EnsureInitialized();
    }

    public StandardControlDictionary()
    {
        Source = new Uri(
            "pack://application:,,,/Wpf.Ui;component/Resources/Standard.xaml",
            UriKind.Absolute
        );
    }
}
