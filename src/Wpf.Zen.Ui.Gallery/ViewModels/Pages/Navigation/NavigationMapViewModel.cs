// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

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

    /// <summary>Gets the editable presentation layout retained by this sample view model.</summary>
    /// <remarks>
    /// Keeping coordinates outside <see cref="NavigationMap"/> demonstrates that route semantics can
    /// be shared with the shell while each visual projection owns an independent arrangement.
    /// </remarks>
    public NavigationMapLayout Layout { get; } = new();

    /// <summary>Saves the current presentation layout as a versioned UTF-8 JSON document.</summary>
    /// <param name="fileName">The destination selected by the sample user.</param>
    public void SaveLayout(string fileName)
    {
        var document = new LayoutDocument
        {
            Version = 1,
            Placements = Layout.Placements
                .Select(placement => new PlacementDocument
                {
                    NodeKey = placement.NodeKey,
                    X = placement.Position.X,
                    Y = placement.Position.Y,
                    IsPinned = placement.IsPinned,
                })
                .ToList(),
        };
        var serializer = new DataContractJsonSerializer(typeof(LayoutDocument));
        using var stream = File.Create(fileName);
        serializer.WriteObject(stream, document);
    }

    /// <summary>
    /// Loads a layout document atomically, leaving the active layout untouched when validation or
    /// deserialization fails.
    /// </summary>
    /// <param name="fileName">The JSON file selected by the sample user.</param>
    public void LoadLayout(string fileName)
    {
        var serializer = new DataContractJsonSerializer(typeof(LayoutDocument));
        using var stream = File.OpenRead(fileName);
        var document = (LayoutDocument?)serializer.ReadObject(stream)
            ?? throw new SerializationException("The layout document is empty.");
        if (document.Version != 1)
        {
            throw new SerializationException($"Unsupported navigation-map layout version {document.Version}.");
        }

        var placements = new List<NavigationMapPlacement>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (PlacementDocument source in document.Placements ?? [])
        {
            if (
                string.IsNullOrWhiteSpace(source.NodeKey)
                || !keys.Add(source.NodeKey)
                || double.IsNaN(source.X)
                || double.IsInfinity(source.X)
                || double.IsNaN(source.Y)
                || double.IsInfinity(source.Y)
            )
            {
                throw new SerializationException("The layout contains an invalid or duplicate placement.");
            }

            placements.Add(
                new NavigationMapPlacement(source.NodeKey, new Point(source.X, source.Y))
                {
                    IsPinned = source.IsPinned,
                }
            );
        }

        Layout.Clear();
        foreach (NavigationMapPlacement placement in placements)
        {
            Layout.Placements.Add(placement);
        }
    }

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

    [DataContract]
    private sealed class LayoutDocument
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; }

        [DataMember(Name = "placements", Order = 1)]
        public List<PlacementDocument>? Placements { get; set; }
    }

    [DataContract]
    private sealed class PlacementDocument
    {
        [DataMember(Name = "nodeKey", Order = 0)]
        public string NodeKey { get; set; } = string.Empty;

        [DataMember(Name = "x", Order = 1)]
        public double X { get; set; }

        [DataMember(Name = "y", Order = 2)]
        public double Y { get; set; }

        [DataMember(Name = "isPinned", Order = 3)]
        public bool IsPinned { get; set; }
    }
}