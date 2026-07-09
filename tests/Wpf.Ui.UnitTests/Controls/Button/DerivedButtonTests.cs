// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.ButtonControl;

public class DerivedButtonTests
{
    [Fact]
    public void AliasControls_UseTheirBaseControlStyles()
    {
        RunOnStaThread(
            () =>
            {
                Assert.Equal(
                    typeof(HyperlinkButton),
                    new AnchorProbe().ExposedDefaultStyleKey
                );
                Assert.Equal(
                    typeof(Wpf.Ui.Controls.Button),
                    new CalendarDatePickerProbe().ExposedDefaultStyleKey
                );
            }
        );
    }

    [Fact]
    public void CalendarDatePicker_DefaultIconsAreNotShared()
    {
        RunOnStaThread(
            () =>
            {
                var firstIcon = Assert.IsType<SymbolIcon>(new CalendarDatePicker().Icon);
                var secondIcon = Assert.IsType<SymbolIcon>(new CalendarDatePicker().Icon);

                Assert.Equal(SymbolRegular.CalendarRtl24, firstIcon.Symbol);
                Assert.Equal(SymbolRegular.CalendarRtl24, secondIcon.Symbol);
                Assert.NotSame(firstIcon, secondIcon);

                var customIcon = new SymbolIcon(SymbolRegular.Clock24);
                var calendarDatePicker = new CalendarDatePicker { Icon = customIcon };

                Assert.Same(customIcon, calendarDatePicker.Icon);
            }
        );
    }

    [Theory]
    [InlineData("DropDownButton/DropDownButton.xaml", "DefaultUiDropDownButtonStyle", typeof(DropDownButton))]
    [InlineData("SplitButton/SplitButton.xaml", "DefaultUiSplitButtonStyle", typeof(SplitButton))]
    public void DerivedButtonStyles_LoadWithoutButtonDictionary(
        string relativePath,
        string styleKey,
        Type controlType
    )
    {
        RunOnStaThread(
            () =>
            {
                var resources = new ResourceDictionary
                {
                    Source = new Uri(
                        $"pack://application:,,,/Wpf.Ui;component/Controls/{relativePath}",
                        UriKind.Absolute
                    ),
                };

                var style = Assert.IsType<Style>(resources[styleKey]);

                Assert.Equal(controlType, style.TargetType);
                Assert.Null(style.BasedOn);
                Assert.Contains(
                    style.Setters,
                    setter =>
                        setter is Setter { Property: not null } typedSetter
                        && typedSetter.Property == System.Windows.Controls.Control.TemplateProperty
                );
            }
        );
    }

    [Fact]
    public void GenericTheme_ProvidesAllAffectedControlTemplates()
    {
        RunOnStaThread(
            () =>
            {
                var dropDownButton = new DropDownButton();
                var splitButton = new SplitButton();
                var anchor = new Anchor();
                var calendarDatePicker = new CalendarDatePicker();
                var textBox = new Wpf.Ui.Controls.TextBox();
                var root = new System.Windows.Controls.StackPanel();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/Wpf.Ui;component/Themes/Generic.xaml",
                            UriKind.Absolute
                        ),
                    }
                );
                root.Children.Add(dropDownButton);
                root.Children.Add(splitButton);
                root.Children.Add(anchor);
                root.Children.Add(calendarDatePicker);
                root.Children.Add(textBox);

                Assert.True(dropDownButton.ApplyTemplate());
                Assert.True(splitButton.ApplyTemplate());
                Assert.True(anchor.ApplyTemplate());
                Assert.True(calendarDatePicker.ApplyTemplate());
                Assert.True(textBox.ApplyTemplate());

                var clearButton = Assert.IsType<Wpf.Ui.Controls.Button>(
                    textBox.Template.FindName("ClearButton", textBox)
                );

                Assert.True(clearButton.ApplyTemplate());
                Assert.NotNull(clearButton.Template);
            }
        );
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class AnchorProbe : Anchor
    {
        public object ExposedDefaultStyleKey => DefaultStyleKey;
    }

    private sealed class CalendarDatePickerProbe : CalendarDatePicker
    {
        public object ExposedDefaultStyleKey => DefaultStyleKey;
    }
}
