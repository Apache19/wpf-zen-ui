// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.UnitTests.Controls.ButtonControl;

public class DerivedButtonTests
{
    [Fact]
    public void AliasControls_UseTheirBaseControlStyles()
    {
        RunOnStaThread(
            () =>
            {
                Assert.Equal(
                    typeof(Wpf.Zen.Ui.Controls.ToggleButton),
                    new CalendarDatePickerProbe().ExposedDefaultStyleKey
                );
                Assert.Equal(
                    typeof(DropDownButton),
                    new DropDownButtonProbe().ExposedDefaultStyleKey
                );
            }
        );
    }

    [Fact]
    public void DropDownButton_DoesNotExposeTheLegacyRightIconSlot()
    {
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.Button).GetProperty("RightIcon"));
        Assert.Null(typeof(Wpf.Zen.Ui.Controls.Button).GetField("RightIconProperty"));
    }

    [Fact]
    public void CalendarDatePicker_DefaultIconUsesReusableSourceData()
    {
        RunOnStaThread(
            () =>
            {
                var firstIcon = Assert.IsType<FluentIconSource>(new CalendarDatePicker().Icon);
                var secondIcon = Assert.IsType<FluentIconSource>(new CalendarDatePicker().Icon);

                Assert.Equal(SymbolRegular.CalendarRtl24, firstIcon.Symbol);
                Assert.Equal(SymbolRegular.CalendarRtl24, secondIcon.Symbol);
                Assert.Same(firstIcon, secondIcon);

                var customIcon = new FluentIcon(SymbolRegular.Clock24);
                var calendarDatePicker = new CalendarDatePicker();
                calendarDatePicker.Icon = customIcon;

                Assert.Same(customIcon, calendarDatePicker.Icon);
            }
        );
    }

    [Fact]
    public void CalendarDatePicker_InitializesPopupBeforeFirstClick()
    {
        RunOnStaThread(
            () =>
            {
                // Theme styles are cached process-wide by WPF and cannot safely be reused by the separate STA
                // threads employed by this test fixture. The popup lifecycle does not depend on the default style.
                var calendarDatePicker = new CalendarDatePicker { OverridesDefaultStyle = true };

                calendarDatePicker.BeginInit();
                calendarDatePicker.EndInit();

                var popupField = typeof(CalendarDatePicker).GetField(
                    "_popup",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var popup = Assert.IsType<Popup>(popupField?.GetValue(calendarDatePicker));

                Assert.False(popup.IsOpen);
                Assert.IsType<System.Windows.Controls.Calendar>(popup.Child);
                Assert.Same(calendarDatePicker, popup.PlacementTarget);
            }
        );
    }

    [Fact]
    public void CalendarDatePicker_SynchronizesToggleAndPopupStates()
    {
        RunOnStaThread(
            () =>
            {
                var externalState = new System.Windows.Controls.Primitives.ToggleButton();
                var calendarDatePicker = new CalendarDatePickerProbe();
                _ = calendarDatePicker.SetBinding(
                    CalendarDatePicker.IsCalendarOpenProperty,
                    new Binding(nameof(System.Windows.Controls.Primitives.ToggleButton.IsChecked))
                    {
                        Source = externalState,
                        Mode   = BindingMode.TwoWay,
                    }
                );

                Assert.False(calendarDatePicker.IsCalendarOpen);
                Assert.False(calendarDatePicker.IsChecked);

                calendarDatePicker.IsThreeState = true;
                calendarDatePicker.IsChecked = null;
                Assert.False(calendarDatePicker.IsThreeState);
                Assert.False(calendarDatePicker.IsChecked);

                calendarDatePicker.InvokeClick();
                Assert.True(calendarDatePicker.IsCalendarOpen);
                Assert.True(externalState.IsChecked);
                Assert.True(
                    BindingOperations.IsDataBound(
                        calendarDatePicker,
                        CalendarDatePicker.IsCalendarOpenProperty
                    )
                );

                calendarDatePicker.InvokeClick();
                Assert.False(calendarDatePicker.IsCalendarOpen);
                Assert.False(calendarDatePicker.IsChecked);
                Assert.False(externalState.IsChecked);
            }
        );
    }

    [Fact]
    public void SplitButtonStyle_LoadsWithoutButtonDictionary()
    {
        RunOnStaThread(
            () =>
            {
                var resources = new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/Wpf.Zen.Ui;component/Controls/SplitButton/SplitButton.xaml",
                        UriKind.Absolute
                    ),
                };

                var style = Assert.IsType<Style>(resources[StyleKeys.UiSplitButtonStyle]);
                var appearanceStyle = Assert.IsType<Style>(style.BasedOn);

                Assert.Equal(typeof(SplitButton), style.TargetType);
                Assert.Equal(typeof(IFrameworkInputElement), appearanceStyle.TargetType);
                Assert.Contains(
                    appearanceStyle.Setters,
                    setter =>
                        setter is Setter { Property: not null } typedSetter
                        && typedSetter.Property
                            == Wpf.Zen.Ui.Controls.Button.MouseOverBackgroundProperty
                );
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
                var hyperlinkButton = new HyperlinkButton();
                var calendarDatePicker = new CalendarDatePicker();
                var toggleButton = new Wpf.Zen.Ui.Controls.ToggleButton();
                toggleButton.Icon = new FluentIcon(SymbolRegular.Calendar24);
                var button = new Wpf.Zen.Ui.Controls.Button();
                var textBox = new Wpf.Zen.Ui.Controls.TextBox();
                var root = new System.Windows.Controls.StackPanel();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                            UriKind.Absolute
                        ),
                    }
                );
                root.Children.Add(dropDownButton);
                root.Children.Add(splitButton);
                root.Children.Add(hyperlinkButton);
                root.Children.Add(calendarDatePicker);
                root.Children.Add(toggleButton);
                root.Children.Add(button);
                root.Children.Add(textBox);

                Assert.True(dropDownButton.ApplyTemplate());
                Assert.True(splitButton.ApplyTemplate());
                Assert.True(hyperlinkButton.ApplyTemplate());
                Assert.True(calendarDatePicker.ApplyTemplate());
                Assert.True(toggleButton.ApplyTemplate());
                Assert.True(button.ApplyTemplate());
                Assert.True(textBox.ApplyTemplate());

                Assert.Null(button.Template.FindName("rightIconPresenter", button));
                var dropDownChevron = Assert.IsType<FluentIcon>(
                    dropDownButton.Template.FindName("DropDownChevron", dropDownButton)
                );
                Assert.Equal(SymbolRegular.ChevronDown24, dropDownChevron.Symbol);
                Assert.Equal(Visibility.Visible, dropDownChevron.Visibility);

                dropDownButton.IsDropDownArrowVisible = false;
                Assert.Equal(Visibility.Collapsed, dropDownChevron.Visibility);

                var toggleButtonIconPresenter =
                    Assert.IsAssignableFrom<System.Windows.Controls.ContentPresenter>(
                        toggleButton.Template.FindName("iconPresenter", toggleButton)
                    );

                Assert.Equal(Visibility.Visible, toggleButtonIconPresenter.Visibility);
                Assert.Same(toggleButton.Icon, toggleButtonIconPresenter.Content);

                var calendarDatePickerIconPresenter =
                    Assert.IsAssignableFrom<System.Windows.Controls.ContentPresenter>(
                        calendarDatePicker.Template.FindName(
                            "iconPresenter",
                            calendarDatePicker
                        )
                    );

                Assert.Same(
                    calendarDatePicker.Icon,
                    calendarDatePickerIconPresenter.Content
                );

                var clearButton = Assert.IsType<Wpf.Zen.Ui.Controls.Button>(
                    textBox.Template.FindName("ClearButton", textBox)
                );

                Assert.True(clearButton.ApplyTemplate());
                Assert.NotNull(clearButton.Template);
            }
        );
    }

    private static void RunOnStaThread(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class CalendarDatePickerProbe : CalendarDatePicker
    {
        public object ExposedDefaultStyleKey => DefaultStyleKey;

        public void InvokeClick()
        {
            OnClick();
        }
    }

    private sealed class DropDownButtonProbe : DropDownButton
    {
        public object ExposedDefaultStyleKey => DefaultStyleKey;
    }
}
