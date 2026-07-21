// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Microsoft.Extensions.Localization;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Resources;
using Wpf.Ui.Gallery.Views.Pages;
using Wpf.Ui.Gallery.Views.Pages.BasicInput;
using Wpf.Ui.Gallery.Views.Pages.Collections;
using Wpf.Ui.Gallery.Views.Pages.DateAndTime;
using Wpf.Ui.Gallery.Views.Pages.DesignGuidance;
using Wpf.Ui.Gallery.Views.Pages.DialogsAndFlyouts;
using Wpf.Ui.Gallery.Views.Pages.Layout;
using Wpf.Ui.Gallery.Views.Pages.Media;
using Wpf.Ui.Gallery.Views.Pages.Navigation;
using Wpf.Ui.Gallery.Views.Pages.OpSystem;
using Wpf.Ui.Gallery.Views.Pages.StatusAndInfo;
using Wpf.Ui.Gallery.Views.Pages.Text;
using Wpf.Ui.Gallery.Views.Pages.Windows;
using UiSeparator = Wpf.Ui.Controls.Separator;

// ReSharper disable UnusedMember.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Wpf.Ui.Gallery.ViewModels.Windows;

public partial class MainWindowViewModel(IStringLocalizer<Translations> localizer) : ViewModel
{
    [ObservableProperty]
    public partial string ApplicationTitle { get; set; } = localizer["WPF UI Gallery"];

    [ObservableProperty]
    public partial ObservableCollection<object> MenuItems { get; set; } =
    [
        new NavigationViewItem("Home", SymbolRegular.Home24, typeof(DashboardPage)),
        new NavigationViewItem("Design guidance", SymbolRegular.DesignIdeas24)
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Typography", SymbolRegular.TextFont24,  typeof(TypographyPage)),
                new NavigationViewItem("Icons",      SymbolRegular.Diversity24, typeof(IconsPage)),
                new NavigationViewItem("Colors",     SymbolRegular.Color24,     typeof(ColorsPage)),
            },
        },
        new NavigationViewItem("All samples", SymbolRegular.List24, typeof(AllControlsPage)),
        new UiSeparator(),
        new NavigationViewItem("Basic Input", SymbolRegular.CheckboxChecked24, typeof(BasicInputPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Anchor",          typeof(AnchorPage)), new NavigationViewItem("Button", typeof(ButtonPage)),
                new NavigationViewItem("DropDownButton",  typeof(DropDownButtonPage)),
                new NavigationViewItem("HyperlinkButton", typeof(HyperlinkButtonPage)),
                new NavigationViewItem("ToggleButton",    typeof(ToggleButtonPage)), new NavigationViewItem("ToggleSwitch", typeof(ToggleSwitchPage)),
                new NavigationViewItem("CheckBox",        typeof(CheckBoxPage)), new NavigationViewItem("ComboBox",         typeof(ComboBoxPage)),
                new NavigationViewItem("RadioButton",     typeof(RadioButtonPage)), new NavigationViewItem("RatingControl", typeof(RatingPage)),
                new NavigationViewItem("ThumbRate",       typeof(ThumbRatePage)), new NavigationViewItem("SplitButton",     typeof(SplitButtonPage)),
                new NavigationViewItem("Slider",          typeof(SliderPage))
            },
        },
        new NavigationViewItem("Collections", SymbolRegular.Table24, typeof(CollectionsPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("DataGrid", typeof(DataGridPage)), new NavigationViewItem("ListBox",  typeof(ListBoxPage)),
                new NavigationViewItem("ListView", typeof(ListViewPage)), new NavigationViewItem("TreeView", typeof(TreeViewPage)),
                #if DEBUG
                new NavigationViewItem("TreeList", typeof(TreeListPage)),
                #endif
            },
        },
        new NavigationViewItem("Date & time", SymbolRegular.CalendarClock24, typeof(DateAndTimePage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("CalendarDatePicker", typeof(CalendarDatePickerPage)),
                new NavigationViewItem("Calendar",           typeof(CalendarPage)), new NavigationViewItem("DatePicker", typeof(DatePickerPage)),
                new NavigationViewItem("TimePicker",         typeof(TimePickerPage)),
            },
        },
        new NavigationViewItem("Dialogs & flyouts", SymbolRegular.Chat24, typeof(DialogsAndFlyoutsPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Snackbars", typeof(SnackbarPage)), new NavigationViewItem("ContentDialog", typeof(ContentDialogPage)),
                new NavigationViewItem("Flyout",    typeof(FlyoutPage)), new NavigationViewItem("MessageBox",      typeof(MessageBoxPage)),
            },
        },
        #if DEBUG
        new NavigationViewItem("Layout", SymbolRegular.News24, typeof(LayoutPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Expander",   typeof(ExpanderPage)), new NavigationViewItem("CardControl", typeof(CardControlPage)),
                new NavigationViewItem("CardAction", typeof(CardActionPage)),
            },
        },
        #endif
        new NavigationViewItem("Media", SymbolRegular.PlayCircle24, typeof(MediaPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Image",   typeof(ImagePage)), new NavigationViewItem("Canvas",       typeof(CanvasPage)),
                new NavigationViewItem("WebView", typeof(WebViewPage)), new NavigationViewItem("WebBrowser", typeof(WebBrowserPage)),
            },
        },
        new NavigationViewItem("Navigation", SymbolRegular.Navigation24, typeof(NavigationPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("BreadcrumbBar",         typeof(BreadcrumbBarPage)),
                new NavigationViewItem("NavigationView",        typeof(NavigationViewPage)), new NavigationViewItem("Menu", typeof(MenuPage)),
                new NavigationViewItem("Multilevel navigation", typeof(MultilevelNavigationPage)),
                new NavigationViewItem("TabControl",            typeof(TabControlPage)),
            },
        },
        new NavigationViewItem("Status & info", SymbolRegular.ChatBubblesQuestion24, typeof(StatusAndInfoPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("InfoBadge",   typeof(InfoBadgePage)), new NavigationViewItem("InfoBar",        typeof(InfoBarPage)),
                new NavigationViewItem("ProgressBar", typeof(ProgressBarPage)), new NavigationViewItem("ProgressRing", typeof(ProgressRingPage)),
                new NavigationViewItem("ToolTip",     typeof(ToolTipPage)),
            },
        },
        new NavigationViewItem("Text", SymbolRegular.DrawText24, typeof(TextPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("AutoSuggestBox", typeof(AutoSuggestBoxPage)), new NavigationViewItem("NumberBox", typeof(NumberBoxPage)),
                new NavigationViewItem("PasswordBox",    typeof(PasswordBoxPage)), new NavigationViewItem("RichTextBox",  typeof(RichTextBoxPage)),
                new NavigationViewItem("Label",          typeof(LabelPage)), new NavigationViewItem("TextBlock",          typeof(TextBlockPage)),
                new NavigationViewItem("TextBox",        typeof(TextBoxPage)),
            },
        },
        new NavigationViewItem("System", SymbolRegular.Desktop24, typeof(OpSystemPage))
        {
            ItemsSource = new object[]
            {
                new NavigationViewItem("Clipboard", typeof(ClipboardPage)), new NavigationViewItem("FilePicker", typeof(FilePickerPage)),
            },
        },
        new NavigationViewItem("Windows", SymbolRegular.WindowApps24, typeof(WindowsPage)),
    ];

    [ObservableProperty]
    public partial ObservableCollection<object> FooterMenuItems { get; set; } =
    [
        new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(SettingsPage)),
    ];

    [ObservableProperty]
    public partial ObservableCollection<Control> TrayMenuItems { get; set; } =
    [
        new MenuItem()
        {
            Header = "Home",
            Tag    = "tray_home",
            Icon = new SymbolIcon
            {
                Symbol = SymbolRegular.Home24
            },
        },
        new MenuItem()
        {
            Header = "Settings",
            Tag    = "tray_settings",
            Icon = new SymbolIcon
            {
                Symbol = SymbolRegular.Settings24
            },
        },
        new UiSeparator(),
        new MenuItem()
        {
            Header = "Close",
            Tag    = "tray_close",
            Icon = new SymbolIcon
            {
                Symbol = SymbolRegular.Dismiss24
            },
        }
    ];
}