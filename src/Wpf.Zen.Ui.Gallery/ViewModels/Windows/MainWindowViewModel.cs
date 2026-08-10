// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Microsoft.Extensions.Localization;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Resources;
using Wpf.Zen.Ui.Gallery.Views.Pages;
using Wpf.Zen.Ui.Gallery.Views.Pages.BasicInput;
using Wpf.Zen.Ui.Gallery.Views.Pages.Collections;
using Wpf.Zen.Ui.Gallery.Views.Pages.DateAndTime;
using Wpf.Zen.Ui.Gallery.Views.Pages.DesignGuidance;
using Wpf.Zen.Ui.Gallery.Views.Pages.DialogsAndFlyouts;
using Wpf.Zen.Ui.Gallery.Views.Pages.Layout;
using Wpf.Zen.Ui.Gallery.Views.Pages.Media;
using Wpf.Zen.Ui.Gallery.Views.Pages.Navigation;
using Wpf.Zen.Ui.Gallery.Views.Pages.OpSystem;
using Wpf.Zen.Ui.Gallery.Views.Pages.StatusAndInfo;
using Wpf.Zen.Ui.Gallery.Views.Pages.Text;
using Wpf.Zen.Ui.Gallery.Views.Pages.Windows;
using UiSeparator = Wpf.Zen.Ui.Controls.Separator;

// ReSharper disable UnusedMember.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Wpf.Zen.Ui.Gallery.ViewModels.Windows;

public partial class MainWindowViewModel(IStringLocalizer<Translations> localizer) : ViewModel
{
    /// <summary>
    /// Gets or sets the presentation mode of this window's navigation view.
    /// </summary>
    /// <remarks>
    /// Navigation appearance is window state, not a navigation-service operation. Keeping it on
    /// the window view model lets the settings page update the correct scoped shell through normal
    /// binding without exposing the concrete NavigationView from INavigationService.
    /// </remarks>
    [ObservableProperty]
    public partial NavigationViewPaneDisplayMode NavigationPaneDisplayMode { get; set; } =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    public partial string ApplicationTitle { get; set; } = localizer["WPF Zen UI Gallery"];

    [ObservableProperty]
    public partial ObservableCollection<object> MenuItems { get; set; } =
    [
        new NavigationItem("Home", SymbolRegular.Home24, typeof(DashboardPage)),
        new NavigationItem("Design guidance", SymbolRegular.DesignIdeas24)
        {
            Children = new object[]
            {
                new NavigationItem("Typography", SymbolRegular.TextFont24,  typeof(TypographyPage)),
                new NavigationItem("Icons",      SymbolRegular.Diversity24, typeof(IconsPage)),
                new NavigationItem("Colors",     SymbolRegular.Color24,     typeof(ColorsPage)),
            },
        },
        new NavigationItem("All samples", SymbolRegular.List24, typeof(AllControlsPage)),
        new UiSeparator(),
        new NavigationItem("Basic Input", SymbolRegular.CheckboxChecked24, typeof(BasicInputPage))
        {
            Children = new object[]
            {
                new NavigationItem("Anchor",          typeof(AnchorPage)), new NavigationItem("Button", typeof(ButtonPage)),
                new NavigationItem("DropDownButton",  typeof(DropDownButtonPage)),
                new NavigationItem("HyperlinkButton", typeof(HyperlinkButtonPage)),
                new NavigationItem("ToggleButton",    typeof(ToggleButtonPage)), new NavigationItem("ToggleSwitch", typeof(ToggleSwitchPage)),
                new NavigationItem("CheckBox",        typeof(CheckBoxPage)), new NavigationItem("ComboBox",         typeof(ComboBoxPage)),
                new NavigationItem("RadioButton",     typeof(RadioButtonPage)), new NavigationItem("RatingControl", typeof(RatingPage)),
                new NavigationItem("ThumbRate",       typeof(ThumbRatePage)), new NavigationItem("SplitButton",     typeof(SplitButtonPage)),
                new NavigationItem("Slider",          typeof(SliderPage))
            },
        },
        new NavigationItem("Collections", SymbolRegular.Table24, typeof(CollectionsPage))
        {
            Children = new object[]
            {
                new NavigationItem("DataGrid", typeof(DataGridPage)), new NavigationItem("ListBox",  typeof(ListBoxPage)),
                new NavigationItem("ListView", typeof(ListViewPage)), new NavigationItem("TreeView", typeof(TreeViewPage)),
                #if DEBUG
                new NavigationItem("TreeList", typeof(TreeListPage)),
                #endif
            },
        },
        new NavigationItem("Date & time", SymbolRegular.CalendarClock24, typeof(DateAndTimePage))
        {
            Children = new object[]
            {
                new NavigationItem("CalendarDatePicker", typeof(CalendarDatePickerPage)),
                new NavigationItem("Calendar",           typeof(CalendarPage)), new NavigationItem("DatePicker", typeof(DatePickerPage)),
                new NavigationItem("TimePicker",         typeof(TimePickerPage)),
            },
        },
        new NavigationItem("Dialogs & flyouts", SymbolRegular.Chat24, typeof(DialogsAndFlyoutsPage))
        {
            Children = new object[]
            {
                new NavigationItem("Snackbars", typeof(SnackbarPage)), new NavigationItem("ContentDialog", typeof(ContentDialogPage)),
                new NavigationItem("Flyout",    typeof(FlyoutPage)), new NavigationItem("MessageBox",      typeof(MessageBoxPage)),
            },
        },
        #if DEBUG
        new NavigationItem("Layout", SymbolRegular.News24, typeof(LayoutPage))
        {
            Children = new object[]
            {
                new NavigationItem("Expander",   typeof(ExpanderPage)), new NavigationItem("CardControl", typeof(CardControlPage)),
                new NavigationItem("CardAction", typeof(CardActionPage)),
            },
        },
        #endif
        new NavigationItem("Media", SymbolRegular.PlayCircle24, typeof(MediaPage))
        {
            Children = new object[]
            {
                new NavigationItem("Image",   typeof(ImagePage)), new NavigationItem("Canvas",       typeof(CanvasPage)),
                new NavigationItem("WebView", typeof(WebViewPage)), new NavigationItem("WebBrowser", typeof(WebBrowserPage)),
            },
        },
        new NavigationItem("Navigation", SymbolRegular.Navigation24, typeof(NavigationPage))
        {
            Children = new object[]
            {
                new NavigationItem("BreadcrumbBar",         typeof(BreadcrumbBarPage)),
                new NavigationItem("NavigationView",        typeof(NavigationViewPage)), new NavigationItem("Menu", typeof(MenuPage)),
                new NavigationItem("Multilevel navigation", typeof(MultilevelNavigationPage)),
                new NavigationItem("TabControl",            typeof(TabControlPage)),
            },
        },
        new NavigationItem("Status & info", SymbolRegular.ChatBubblesQuestion24, typeof(StatusAndInfoPage))
        {
            Children = new object[]
            {
                new NavigationItem("InfoBadge",   typeof(InfoBadgePage)), new NavigationItem("InfoBar",        typeof(InfoBarPage)),
                new NavigationItem("ProgressBar", typeof(ProgressBarPage)), new NavigationItem("ProgressRing", typeof(ProgressRingPage)),
                new NavigationItem("ToolTip",     typeof(ToolTipPage)),
            },
        },
        new NavigationItem("Text", SymbolRegular.DrawText24, typeof(TextPage))
        {
            Children = new object[]
            {
                new NavigationItem("AutoSuggestBox", typeof(AutoSuggestBoxPage)), new NavigationItem("NumberBox", typeof(NumberBoxPage)),
                new NavigationItem("PasswordBox",    typeof(PasswordBoxPage)), new NavigationItem("RichTextBox",  typeof(RichTextBoxPage)),
                new NavigationItem("Label",          typeof(LabelPage)), new NavigationItem("TextBlock",          typeof(TextBlockPage)),
                new NavigationItem("TextBox",        typeof(TextBoxPage)),
            },
        },
        new NavigationItem("System", SymbolRegular.Desktop24, typeof(OpSystemPage))
        {
            Children = new object[]
            {
                new NavigationItem("Clipboard", typeof(ClipboardPage)), new NavigationItem("FilePicker", typeof(FilePickerPage)),
            },
        },
        new NavigationItem("Windows", SymbolRegular.WindowApps24, typeof(WindowsPage)),
    ];

    [ObservableProperty]
    public partial ObservableCollection<object> FooterMenuItems { get; set; } =
    [
        new NavigationItem("Settings", SymbolRegular.Settings24, typeof(SettingsPage)),
    ];

    [ObservableProperty]
    public partial ObservableCollection<Control> TrayMenuItems { get; set; } =
    [
        new MenuItem()
        {
            Header = "Home",
            Tag    = "tray_home",
            Icon = new FluentIconSource(SymbolRegular.Home24),
        },
        new MenuItem()
        {
            Header = "Settings",
            Tag    = "tray_settings",
            Icon = new FluentIconSource(SymbolRegular.Settings24),
        },
        new UiSeparator(),
        new MenuItem()
        {
            Header = "Close",
            Tag    = "tray_close",
            Icon = new FluentIconSource(SymbolRegular.Dismiss24),
        }
    ];
}
