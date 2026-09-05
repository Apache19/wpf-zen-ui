// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;
using System.Windows.Input;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Contextual popup.", SymbolRegular.AppTitle24)]
public partial class FlyoutPage : INavigableView<FlyoutViewModel>
{
    public FlyoutViewModel ViewModel { get; }

    public FlyoutPage(FlyoutViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    private void OnShowDetailsClick(object sender, RoutedEventArgs e) => ToggleDetails((FrameworkElement)sender);

    private void OnAnchorMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ToggleDetails((FrameworkElement)sender);
        e.Handled = true;
    }

    private void OnAnchorKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            ToggleDetails((FrameworkElement)sender);
            e.Handled = true;
        }
    }

    private void ToggleDetails(FrameworkElement target)
    {
        var flyout = (Flyout)Resources["DetailsFlyout"];
        if (flyout.IsOpen && ReferenceEquals(flyout.PlacementTarget, target)) flyout.Hide();
        else flyout.ShowAt(target);
    }

    private void OnCloseDetailsClick(object sender, RoutedEventArgs e) => ((Flyout)Resources["DetailsFlyout"]).Hide();

    private void OnDestinationGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var flyout = (Flyout)Resources["TextSelectionFlyout"];

        // Flyout returns focus to its target on selection or Escape. That transition must not
        // reopen the panel, including when IsOpen is already false during the focus callback.
        if (e.OldFocus is DependencyObject previousFocus && flyout.IsAncestorOf(previousFocus))
        {
            return;
        }

        // Finish the TextBox focus transition before Flyout moves focus into its first option.
        // The queued request is ignored if navigation or another input has already moved focus away.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
        {
            if (DestinationTextBox.IsKeyboardFocused && !flyout.IsOpen)
            {
                flyout.ShowAt(DestinationTextBox);
            }
        }));
    }

    private void OnDestinationClick(object sender, RoutedEventArgs e)
    {
        var option = (System.Windows.Controls.Button)sender;
        DestinationTextBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, (string)option.Content);
        ((Flyout)Resources["TextSelectionFlyout"]).Hide();
        DestinationTextBox.CaretIndex = DestinationTextBox.Text.Length;
    }
}
