// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.Diagnostics;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Text;

/// <summary>
/// Demonstrates the embedded soft keyboard and its native IME diagnostic tools.
/// </summary>
[GalleryPage("Embedded, focus-preserving touch keyboard for WPF editors.", SymbolRegular.Keyboard24)]
public partial class SoftKeyboardPage : INavigableView<SoftKeyboardViewModel>
{
    private CandidateWindowBoundsTracker? _candidateWindowBoundsTracker;

    /// <summary>
    /// Gets the view model used by the soft-keyboard gallery page.
    /// </summary>
    public SoftKeyboardViewModel ViewModel { get; }

    /// <summary>
    /// Initializes the soft-keyboard gallery page.
    /// </summary>
    /// <param name="viewModel">The page view model supplied by dependency injection.</param>
    public SoftKeyboardPage(SoftKeyboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    private void OnCandidateWindowBoundsChecked(object sender, RoutedEventArgs e)
    {
        _candidateWindowBoundsTracker ??= CreateCandidateWindowBoundsTracker();
        _candidateWindowBoundsTracker.Start();
    }

    private void OnCandidateWindowBoundsUnchecked(object sender, RoutedEventArgs e)
    {
        _candidateWindowBoundsTracker?.Stop();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CandidateWindowBoundsCheckBox.IsChecked = false;
        _candidateWindowBoundsTracker?.Dispose();
        _candidateWindowBoundsTracker = null;
    }

    /// <summary>
    /// Creates the page-owned native-window diagnostic and forwards snapshots to the visible status text.
    /// </summary>
    private CandidateWindowBoundsTracker CreateCandidateWindowBoundsTracker()
    {
        var tracker = new CandidateWindowBoundsTracker();
        tracker.SnapshotChanged += (_, description) =>
            CandidateWindowDiagnosticStatus.Text = description;
        return tracker;
    }
}
