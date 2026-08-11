// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

public partial class MultilevelNavigationSample(INavigationService navigationService)
{
    [RelayCommand]
    private void NavigateForward(Type type)
    {
        // The map owns Page 1 → Page 2 → Page 3. Navigation only selects the destination; it does
        // not mutate a hidden hierarchy stack or derive parentage from the page we came from.
        _ = navigationService.Navigate(type);
    }

    [RelayCommand]
    private void NavigateBack()
    {
        _ = navigationService.GoBack();
    }
}
