// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DesignGuidance;

/// <summary>
/// Supports icon catalogue search, selection and copyable XAML examples.
/// </summary>
public partial class IconsViewModel : ViewModel
{
    /// <summary>
    /// Gets the selected family, symbol and variant as one reusable description.
    /// </summary>
    public FontIconSource SelectedIcon => new FluentIconSource(SelectedSymbol, IsIconFilled);

    partial void OnIsIconFilledChanged(bool value)
    {
        UpdateSymbolData();
    }

    private int _selectedIconId = 0;

    private string _autoSuggestBoxText = string.Empty;

    /// <summary>
    /// Gets or sets the selected Fluent symbol.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIcon))]
    public partial FluentSymbolRegular SelectedSymbol { get; set; } = FluentSymbolRegular.Empty;

    /// <summary>
    /// Gets or sets the selected symbol name.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedSymbolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hexadecimal code point displayed in the details.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedSymbolUnicodePoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the XML character entity displayed in the details.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedSymbolTextGlyph { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the copyable FontIcon markup for the current selection.
    /// </summary>
    [ObservableProperty]
    public partial string SelectedSymbolXaml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the selected source uses the filled font.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedIcon))]
    public partial bool IsIconFilled { get; set; } = false;

    /// <summary>
    /// Gets or sets the complete catalogue.
    /// </summary>
    [ObservableProperty]
    public partial List<DisplayableIcon> IconsCollection { get; set; } = [];

    /// <summary>
    /// Gets or sets the catalogue entries matching the current search.
    /// </summary>
    [ObservableProperty]
    public partial List<DisplayableIcon> FilteredIconsCollection { get; set; } = [];

    /// <summary>
    /// Gets or sets the names offered by search suggestions.
    /// </summary>
    [ObservableProperty]
    public partial List<string> IconNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the query and refreshes the matching catalogue entries.
    /// </summary>
    public string AutoSuggestBoxText
    {
        get => _autoSuggestBoxText;
        set
        {
            _ = SetProperty(ref _autoSuggestBoxText, value);
            UpdateSearchResults(value);
        }
    }

    /// <summary>
    /// Initializes the searchable catalogue from the Fluent symbol enumeration.
    /// </summary>
    public IconsViewModel()
    {
        _ = Task.Run(() =>
        {
            var id = 0;
            var names = Enum.GetNames(typeof(FluentSymbolRegular));
            var icons = new List<DisplayableIcon>();

            names = names.OrderBy(n => n).ToArray();

            foreach (string iconName in names)
            {
                var icon = (FluentSymbolRegular)Enum.Parse(typeof(FluentSymbolRegular), iconName);
                var source = new FluentIconSource(icon);

                icons.Add(
                    new DisplayableIcon
                    {
                        Id = id++,
                        Name = iconName,
                        Icon = icon,
                        Symbol = source.Glyph,
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

    /// <summary>
    /// Selects a catalogue item and refreshes its details and font source.
    /// </summary>
    /// <param name="parameter">The catalogue identity to select.</param>
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
        string variant = IsIconFilled ? ", Filled=True" : string.Empty;
        SelectedSymbolXaml =
            $"<ui:FontIcon Source=\"{{ui:FluentIconSource {selectedSymbol.Name}{variant}}}\" />";
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
