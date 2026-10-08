// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.BasicInput;

/// <summary>
/// Demonstrates combo-box appearance, grouping, matching rules, and search interactions.
/// </summary>
[GalleryPage("Button with binary choice.", FluentSymbolRegular.Filter16)]
public partial class ComboBoxPage : INavigableView<ComboBoxViewModel>
{
    /// <summary>
    /// Gets the sample data displayed by this page.
    /// </summary>
    public ComboBoxViewModel ViewModel { get; }

    /// <summary>
    /// Creates the page and connects its sample view model.
    /// </summary>
    /// <param name="viewModel">The page's sample data.</param>
    public ComboBoxPage(ComboBoxViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    /// <summary>
    /// Demonstrates committing the query from an application-defined trigger.
    /// </summary>
    private void OnMatchSearchClick(object sender, RoutedEventArgs e)
    {
        SearchComboBox.TryCommitTextSearch();
    }
}
