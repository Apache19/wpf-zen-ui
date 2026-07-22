// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Ui.Gallery.ViewModels.Pages.Text;

public partial class AutoSuggestBoxViewModel : ViewModel
{
    [ObservableProperty]
    public partial List<string> AutoSuggestBoxSuggestions { get; set; } = new()
    {
        "John",
        "Winston",
        "Adrianna",
        "Spencer",
        "Phoebe",
        "Lucas",
        "Carl",
        "Marissa",
        "Brandon",
        "Antoine",
        "Arielle",
        "Arielle",
        "Jamie",
        "Alexzander",
    };

    [ObservableProperty]
    public partial bool ShowClearButtonDisabled { get; set; } = false;
}
