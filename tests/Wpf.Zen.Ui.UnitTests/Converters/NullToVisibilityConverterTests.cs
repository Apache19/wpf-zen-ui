// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using Wpf.Zen.Ui.Converters;

namespace Wpf.Zen.Ui.UnitTests.Converters;

public class NullToVisibilityConverterTests
{
    [Fact]
    public void Convert_EmptyCollection_ReturnsNullValue()
    {
        var converter = new NullToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, Convert(converter, Array.Empty<object>()));
        Assert.Equal(Visibility.Visible, Convert(converter, new[] { new object() }));
    }

    [Fact]
    public void Convert_EmptyLazySequence_ReturnsNullValue()
    {
        var converter = new NullToVisibilityConverter();

        Assert.Equal(Visibility.Collapsed, Convert(converter, Enumerable.Empty<object>()));
        Assert.Equal(Visibility.Visible, Convert(converter, Enumerable.Repeat(new object(), 1)));
    }

    [Fact]
    public void Convert_CanTreatEmptyCollectionAsNonNull()
    {
        var converter = new NullToVisibilityConverter { TreatEmptyCollectionAsNull = false };

        Assert.Equal(Visibility.Visible, Convert(converter, Array.Empty<object>()));
    }

    [Fact]
    public void Convert_EmptyString_RemainsControlledByStringOption()
    {
        var converter = new NullToVisibilityConverter { TreatEmptyCollectionAsNull = true };

        Assert.Equal(Visibility.Visible, Convert(converter, string.Empty));

        converter.TreatEmptyStringAsNull = true;

        Assert.Equal(Visibility.Collapsed, Convert(converter, string.Empty));
    }

    [Fact]
    public void ObservableCollectionCountBinding_UpdatesAfterCollectionChanges()
    {
        WpfTestThread.Run(
            () =>
            {
                var items = new ObservableCollection<object>();
                var target = new Border();
                var converter = new NullToVisibilityConverter { TreatZeroAsNull = true };

                BindingOperations.SetBinding(
                    target,
                    UIElement.VisibilityProperty,
                    new Binding(nameof(ObservableCollection<object>.Count))
                    {
                        Source = items,
                        Converter = converter,
                    }
                );

                Assert.Equal(Visibility.Collapsed, target.Visibility);

                items.Add(new object());

                Assert.Equal(Visibility.Visible, target.Visibility);

                items.Clear();

                Assert.Equal(Visibility.Collapsed, target.Visibility);
            }
        );
    }

    [Fact]
    public void Convert_ZeroRemainsNonNullUnlessEnabled()
    {
        var converter = new NullToVisibilityConverter();

        Assert.Equal(Visibility.Visible, Convert(converter, 0));

        converter.TreatZeroAsNull = true;

        Assert.Equal(Visibility.Collapsed, Convert(converter, 0));
        Assert.Equal(Visibility.Visible, Convert(converter, 1));
    }

    private static Visibility Convert(NullToVisibilityConverter converter, object? value)
    {
        return Assert.IsType<Visibility>(
            converter.Convert(value, typeof(Visibility), null, CultureInfo.InvariantCulture)
        );
    }
}
