// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
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
                    typeof(HyperlinkButton),
                    new AnchorProbe().ExposedDefaultStyleKey
                );
                Assert.Equal(
                    typeof(Wpf.Zen.Ui.Controls.ToggleButton),
                    new CalendarDatePickerProbe().ExposedDefaultStyleKey
                );
                Assert.Equal(
                    typeof(Wpf.Zen.Ui.Controls.Button),
                    new DropDownButtonProbe().ExposedDefaultStyleKey
                );
            }
        );
    }

    [Fact]
    public void DropDownButton_DefaultRightIconsAreNotShared()
    {
        RunOnStaThread(
            () =>
            {
                var first = new DropDownButton();
                var second = new DropDownButton();
                var firstIcon = Assert.IsType<FluentIcon>(first.RightIcon);
                var secondIcon = Assert.IsType<FluentIcon>(second.RightIcon);

                Assert.Equal(SymbolRegular.ChevronDown24, firstIcon.Symbol);
                Assert.Equal(SymbolRegular.ChevronDown24, secondIcon.Symbol);
                Assert.NotSame(firstIcon, secondIcon);

                var customIcon = new FluentIcon(SymbolRegular.Clock24);
                first.RightIcon = customIcon;
                Assert.Same(customIcon, first.RightIcon);

                first.IsDropDownArrowVisible = false;
                Assert.Null(first.RightIcon);

                first.IsDropDownArrowVisible = true;
                Assert.Same(customIcon, first.RightIcon);
            }
        );
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
                var calendarDatePicker = new CalendarDatePicker { Icon = customIcon };

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
                var anchor = new Anchor();
                var calendarDatePicker = new CalendarDatePicker();
                var toggleButton = new Wpf.Zen.Ui.Controls.ToggleButton
                {
                    Icon = new FluentIcon(SymbolRegular.Calendar24),
                };
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
                root.Children.Add(anchor);
                root.Children.Add(calendarDatePicker);
                root.Children.Add(toggleButton);
                root.Children.Add(button);
                root.Children.Add(textBox);

                Assert.True(dropDownButton.ApplyTemplate());
                Assert.True(splitButton.ApplyTemplate());
                Assert.True(anchor.ApplyTemplate());
                Assert.True(calendarDatePicker.ApplyTemplate());
                Assert.True(toggleButton.ApplyTemplate());
                Assert.True(button.ApplyTemplate());
                Assert.True(textBox.ApplyTemplate());

                var buttonRightIconPresenter = Assert.IsType<System.Windows.Controls.ContentPresenter>(
                    button.Template.FindName("rightIconPresenter", button)
                );
                var dropDownButtonRightIconPresenter =
                    Assert.IsType<System.Windows.Controls.ContentPresenter>(
                        dropDownButton.Template.FindName("rightIconPresenter", dropDownButton)
                    );

                Assert.Equal(Visibility.Collapsed, buttonRightIconPresenter.Visibility);
                Assert.Equal(Visibility.Visible, dropDownButtonRightIconPresenter.Visibility);
                Assert.Same(dropDownButton.RightIcon, dropDownButtonRightIconPresenter.Content);

                var toggleButtonIconPresenter =
                    Assert.IsType<System.Windows.Controls.ContentPresenter>(
                        toggleButton.Template.FindName("iconPresenter", toggleButton)
                    );

                Assert.Equal(Visibility.Visible, toggleButtonIconPresenter.Visibility);
                Assert.Same(toggleButton.Icon, toggleButtonIconPresenter.Content);

                var calendarDatePickerIconPresenter =
                    Assert.IsType<System.Windows.Controls.ContentPresenter>(
                        calendarDatePicker.Template.FindName(
                            "iconPresenter",
                            calendarDatePicker
                        )
                    );

                Assert.Same(calendarDatePicker.Icon, calendarDatePickerIconPresenter.Content);

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
