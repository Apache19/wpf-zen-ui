// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Collections;

public partial class ListViewViewModel : ViewModel
{
    [ObservableProperty]
    public partial int ListViewSelectionModeComboBoxSelectedIndex { get; set; } = 0;

    [ObservableProperty]
    public partial SelectionMode ListViewSelectionMode { get; set; } = SelectionMode.Single;

    [ObservableProperty]
    public partial ObservableCollection<Person> BasicListViewItems { get; set; } = GeneratePersons();

    private static ObservableCollection<Person> GeneratePersons()
    {
        var random = new Random();
        var persons = new ObservableCollection<Person>();

        var names = new[]
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
        var surnames = new[]
        {
            "Doe",
            "Tapia",
            "Cisneros",
            "Lynch",
            "Munoz",
            "Marsh",
            "Hudson",
            "Bartlett",
            "Gregory",
            "Banks",
            "Hood",
            "Fry",
            "Carroll",
        };
        var companies = new[]
        {
            "Pineapple Inc.",
            "Macrosoft Redmond",
            "Amazing Basics Ltd",
            "Megabyte Computers Inc",
            "Roude Mics",
            "XD Projekt Red S.A.",
            "Lepo.co",
        };

        for (int i = 0; i < 50; i++)
        {
            var firstName = names[random.Next(0, names.Length)];
                var lastName = surnames[random.Next(0, surnames.Length)];
                var company = companies[random.Next(0, companies.Length)];

                persons.Add(
                    new Person(
                        firstName,
                        lastName,
                        company,
                        $"{firstName.ToLowerInvariant()}.{lastName.ToLowerInvariant()}@example.com",
                        $"+1 ({random.Next(200, 999)}) {random.Next(100, 999)}-{random.Next(1000, 9999)}"
                    )
                );
        }

        return persons;
    }

    partial void OnListViewSelectionModeComboBoxSelectedIndexChanged(int value)
    {
        ListViewSelectionMode = value switch
        {
            1 => SelectionMode.Multiple,
            2 => SelectionMode.Extended,
            _ => SelectionMode.Single,
        };
    }
}
