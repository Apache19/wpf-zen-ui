// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Models;
using UiCheckBox = Wpf.Ui.Controls.CheckBox;

namespace Wpf.Ui.Gallery.ViewModels.Pages.DesignGuidance;

public partial class IconsViewModel : ViewModel
{
    private int _selectedIconId = 0;

    private string _autoSuggestBoxText = string.Empty;

    [ObservableProperty]
    public partial SymbolRegular SelectedSymbol { get; set; } = SymbolRegular.Empty;

    [ObservableProperty]
    public partial string SelectedSymbolName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSymbolUnicodePoint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSymbolTextGlyph { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedSymbolXaml { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsIconFilled { get; set; } = false;

    [ObservableProperty]
    public partial List<DisplayableIcon> IconsCollection { get; set; } = [];

    [ObservableProperty]
    public partial List<DisplayableIcon> FilteredIconsCollection { get; set; } = [];

    [ObservableProperty]
    public partial List<string> IconNames { get; set; } = [];

    public string AutoSuggestBoxText
    {
        get => _autoSuggestBoxText;
        set
        {
            _ = SetProperty(ref _autoSuggestBoxText, value);
            UpdateSearchResults(value);
        }
    }

    public IconsViewModel()
    {
        _ = Task.Run(() =>
        {
            var id = 0;
            var names = Enum.GetNames(typeof(SymbolRegular));
            var icons = new List<DisplayableIcon>();

            names = names.OrderBy(n => n).ToArray();

            foreach (string iconName in names)
            {
                SymbolRegular icon = SymbolGlyph.Parse(iconName);

                icons.Add(
                    new DisplayableIcon
                    {
                        Id = id++,
                        Name = iconName,
                        Icon = icon,
                        Symbol = ((char)icon).ToString(),
                        Code = ((int)icon).ToString("X4"),
                    }
                );
            }

            IconsCollection = icons;
            FilteredIconsCollection = icons;
            IconNames = icons.Select(icon => icon.Name).ToList();

            if (icons.Count > 4)
            {
                _selectedIconId = 4;

                UpdateSymbolData();
            }
        });
    }

    [RelayCommand]
    public void OnIconSelected(int parameter)
    {
        _selectedIconId = parameter;

        UpdateSymbolData();
    }

    private void UpdateSymbolData()
    {
        if (IconsCollection.Count - 1 < _selectedIconId)
        {
            return;
        }

        DisplayableIcon selectedSymbol = IconsCollection.FirstOrDefault(sym => sym.Id == _selectedIconId);

        SelectedSymbol = selectedSymbol.Icon;
        SelectedSymbolName = selectedSymbol.Name;
        SelectedSymbolUnicodePoint = selectedSymbol.Code;
        SelectedSymbolTextGlyph = $"&#x{selectedSymbol.Code};";
        SelectedSymbolXaml =
            $"<ui:SymbolIcon Symbol=\"{selectedSymbol.Name}\"{(IsIconFilled ? " Filled=\"True\"" : string.Empty)}/>";
    }

    private void UpdateSearchResults(string searchedText)
    {
        _ = Task.Run(() =>
        {
            if (string.IsNullOrEmpty(searchedText))
            {
                FilteredIconsCollection = IconsCollection;

                return true;
            }

            var formattedText = searchedText.ToLower().Trim();

            FilteredIconsCollection = IconsCollection
                .Where(icon => icon.Name.Contains(formattedText, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return true;
        });
    }
}
