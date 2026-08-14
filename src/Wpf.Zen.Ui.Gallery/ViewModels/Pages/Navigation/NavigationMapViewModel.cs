// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.ViewModels.Windows;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

/// <summary>
/// Exposes the primary gallery host's live semantic navigation map and routes invoked map nodes
/// through the same scoped navigation service as the shell.
/// </summary>
/// <remarks>
/// Keeping the original <see cref="Wpf.Zen.Ui.NavigationMap"/> instance is intentional. The sample
/// therefore verifies direct collection binding and observes any runtime changes made by the host;
/// it is not a copied or presentation-only hierarchy.
/// </remarks>
public sealed partial class NavigationMapViewModel(
    MainWindowViewModel mainWindowViewModel,
    INavigationService navigationService) : ViewModel
{
    /// <summary>Gets the navigation map currently owned by the gallery's primary host.</summary>
    public Wpf.Zen.Ui.NavigationMap NavigationMap { get; } = mainWindowViewModel.NavigationMap;

    /// <summary>Navigates to the destination represented by an invokable map node.</summary>
    /// <param name="node">
    /// The node supplied by <see cref="Wpf.Zen.Ui.Controls.NavigationMapView.Command"/>.
    /// </param>
    [RelayCommand(CanExecute = nameof(CanNavigateToNode))]
    private void NavigateToNode(NavigationMapNode node)
    {
        // Structural grouping nodes deliberately have no target. CanExecute keeps those nodes
        // visible in the graph without pretending that they represent a navigable page.
        if (node.Target is { } target)
        {
            _ = navigationService.Navigate(target);
        }
    }

    private bool CanNavigateToNode(NavigationMapNode? node) => node?.Target is not null;
}
