// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Views.Pages.Media;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Media;

public partial class MediaViewModel : ViewModel
{
    [ObservableProperty]
    public partial ICollection<NavigationCard> NavigationCards { get; set; } = new ObservableCollection<NavigationCard>(
        ControlPages
            .FromNamespace(typeof(MediaPage).Namespace!)
            .Select(x => new NavigationCard()
            {
                Name = x.Name,
                Icon = x.Icon,
                Description = x.Description,
                PageType = x.PageType,
            })
    );
}
