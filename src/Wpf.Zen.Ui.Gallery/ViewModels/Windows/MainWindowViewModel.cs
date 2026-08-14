// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Zen.Ui;
using Wpf.Zen.Ui.Controls;
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
using Wpf.Zen.Ui.Gallery.Views.Pages.Samples;
using Wpf.Zen.Ui.Gallery.Views.Pages.StatusAndInfo;
using Wpf.Zen.Ui.Gallery.Views.Pages.Text;
using Wpf.Zen.Ui.Gallery.Views.Pages.Windows;
using UiSeparator = Wpf.Zen.Ui.Controls.Separator;

// ReSharper disable UnusedMember.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Wpf.Zen.Ui.Gallery.ViewModels.Windows;

public partial class MainWindowViewModel : ViewModel
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
    public partial string ApplicationTitle { get; set; } = "WPF Zen UI Gallery";

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class and creates its
    /// semantic navigation map.
    /// </summary>
    /// <remarks>
    /// The semantic graph is authored independently from <see cref="MenuItems"/>. The gallery menu
    /// can therefore be reordered, filtered, or replaced with shortcuts without changing page
    /// parentage, while code-only sample pages still participate in the same map.
    /// </remarks>
    public MainWindowViewModel()
    {
        NavigationMap = CreateNavigationMap();
    }

    /// <summary>Gets the semantic graph used by this window's scoped navigator.</summary>
    public NavigationMap NavigationMap { get; }

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
                new NavigationItem("Anchor",         typeof(AnchorPage)), new NavigationItem("Button",                  typeof(ButtonPage)),
                new NavigationItem("DropDownButton", typeof(DropDownButtonPage)), new NavigationItem("HyperlinkButton", typeof(HyperlinkButtonPage)),
                new NavigationItem("ToggleButton",   typeof(ToggleButtonPage)), new NavigationItem("ToggleSwitch",      typeof(ToggleSwitchPage)),
                new NavigationItem("CheckBox",       typeof(CheckBoxPage)), new NavigationItem("ComboBox",              typeof(ComboBoxPage)),
                new NavigationItem("RadioButton",    typeof(RadioButtonPage)), new NavigationItem("RatingControl",      typeof(RatingPage)),
                new NavigationItem("ThumbRate",      typeof(ThumbRatePage)), new NavigationItem("SplitButton",          typeof(SplitButtonPage)),
                new NavigationItem("Slider",         typeof(SliderPage))
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
                new NavigationItem("CalendarDatePicker", typeof(CalendarDatePickerPage)), new NavigationItem("Calendar", typeof(CalendarPage)),
                new NavigationItem("DatePicker",         typeof(DatePickerPage)), new NavigationItem("TimePicker",       typeof(TimePickerPage)),
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
                new NavigationItem("BreadcrumbBar", typeof(BreadcrumbBarPage)), new NavigationItem("Navigation map", typeof(NavigationMapPage)),
                new NavigationItem("NavigationView", typeof(NavigationViewPage)),
                new NavigationItem("Menu",          typeof(MenuPage)), new NavigationItem("Multilevel navigation", typeof(MultilevelNavigationPage)),
                new NavigationItem("TabControl",    typeof(TabControlPage)),
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
            Icon   = new FluentIconSource(SymbolRegular.Home24),
        },
        new MenuItem()
        {
            Header = "Settings",
            Tag    = "tray_settings",
            Icon   = new FluentIconSource(SymbolRegular.Settings24),
        },
        new UiSeparator(),
        new MenuItem()
        {
            Header = "Close",
            Tag    = "tray_close",
            Icon   = new FluentIconSource(SymbolRegular.Dismiss24),
        }
    ];

    private static NavigationMap CreateNavigationMap()
    {
        var map = new NavigationMap();
        AddPage<DashboardPage>("Home", isBreadcrumbVisible: false);

        const string designGuidance = "gallery.design-guidance";
        AddGroup(designGuidance, "Design guidance", isBreadcrumbVisible: false);
        AddPage<TypographyPage>("Typography", designGuidance);
        AddPage<IconsPage>("Icons", designGuidance);
        AddPage<ColorsPage>("Colors", designGuidance);
        AddPage<AllControlsPage>("All samples");

        string basicInput = AddPage<BasicInputPage>("Basic Input", isBreadcrumbVisible: false);
        AddPage<AnchorPage>("Anchor", basicInput);
        AddPage<ButtonPage>("Button", basicInput);
        AddPage<DropDownButtonPage>("DropDownButton", basicInput);
        AddPage<HyperlinkButtonPage>("HyperlinkButton", basicInput);
        AddPage<ToggleButtonPage>("ToggleButton", basicInput);
        AddPage<ToggleSwitchPage>("ToggleSwitch", basicInput);
        AddPage<CheckBoxPage>("CheckBox", basicInput);
        AddPage<ComboBoxPage>("ComboBox", basicInput);
        AddPage<RadioButtonPage>("RadioButton", basicInput);
        AddPage<RatingPage>("RatingControl", basicInput);
        AddPage<ThumbRatePage>("ThumbRate", basicInput);
        AddPage<SplitButtonPage>("SplitButton", basicInput);
        AddPage<SliderPage>("Slider", basicInput);

        string collections = AddPage<CollectionsPage>("Collections", isBreadcrumbVisible: false);
        AddPage<DataGridPage>("DataGrid", collections);
        AddPage<ListBoxPage>("ListBox", collections);
        AddPage<ListViewPage>("ListView", collections);
        AddPage<TreeViewPage>("TreeView", collections);
        #if DEBUG
        AddPage<TreeListPage>("TreeList", collections);
        #endif

        string dateAndTime = AddPage<DateAndTimePage>("Date & time", isBreadcrumbVisible: false);
        AddPage<CalendarDatePickerPage>("CalendarDatePicker", dateAndTime);
        AddPage<CalendarPage>("Calendar", dateAndTime);
        AddPage<DatePickerPage>("DatePicker", dateAndTime);
        AddPage<TimePickerPage>("TimePicker", dateAndTime);

        string dialogs = AddPage<DialogsAndFlyoutsPage>("Dialogs & flyouts", isBreadcrumbVisible: false);
        AddPage<SnackbarPage>("Snackbars", dialogs);
        AddPage<ContentDialogPage>("ContentDialog", dialogs);
        AddPage<FlyoutPage>("Flyout", dialogs);
        AddPage<MessageBoxPage>("MessageBox", dialogs);

        #if DEBUG
        string layout = AddPage<LayoutPage>("Layout", isBreadcrumbVisible: false);
        AddPage<ExpanderPage>("Expander", layout);
        AddPage<CardControlPage>("CardControl", layout);
        AddPage<CardActionPage>("CardAction", layout);
        #endif

        string media = AddPage<MediaPage>("Media", isBreadcrumbVisible: false);
        AddPage<ImagePage>("Image", media);
        AddPage<CanvasPage>("Canvas", media);
        AddPage<WebViewPage>("WebView", media);
        AddPage<WebBrowserPage>("WebBrowser", media);

        string navigation = AddPage<NavigationPage>("Navigation", isBreadcrumbVisible: false);
        AddPage<BreadcrumbBarPage>("BreadcrumbBar", navigation);
        AddPage<NavigationMapPage>("Navigation map", navigation);
        AddPage<NavigationViewPage>("NavigationView", navigation);
        AddPage<MenuPage>("Menu", navigation);
        string multilevel = AddPage<MultilevelNavigationPage>("Multilevel navigation", navigation);
        AddPage<TabControlPage>("TabControl", navigation);

        // These code-only pages demonstrate that map membership does not require a menu shortcut.
        string page1 = AddPage<MultilevelNavigationSamplePage1>("Page 1", multilevel);
        string page2 = AddPage<MultilevelNavigationSamplePage2>("Page 2", page1);
        AddPage<MultilevelNavigationSamplePage3>("Page 3", page2);

        string status = AddPage<StatusAndInfoPage>("Status & info", isBreadcrumbVisible: false);
        AddPage<InfoBadgePage>("InfoBadge", status);
        AddPage<InfoBarPage>("InfoBar", status);
        AddPage<ProgressBarPage>("ProgressBar", status);
        AddPage<ProgressRingPage>("ProgressRing", status);
        AddPage<ToolTipPage>("ToolTip", status);

        string text = AddPage<TextPage>("Text", isBreadcrumbVisible: false);
        AddPage<AutoSuggestBoxPage>("AutoSuggestBox", text);
        AddPage<NumberBoxPage>("NumberBox", text);
        AddPage<PasswordBoxPage>("PasswordBox", text);
        AddPage<RichTextBoxPage>("RichTextBox", text);
        AddPage<LabelPage>("Label", text);
        AddPage<TextBlockPage>("TextBlock", text);
        AddPage<TextBoxPage>("TextBox", text);

        string system = AddPage<OpSystemPage>("System", isBreadcrumbVisible: false);
        AddPage<ClipboardPage>("Clipboard", system);
        AddPage<FilePickerPage>("FilePicker", system);
        AddPage<WindowsPage>("Windows", isBreadcrumbVisible: false);
        AddPage<SettingsPage>("Settings", isBreadcrumbVisible: false);

        return map;

        void AddGroup(string key, object? content, string? parentKey = null, bool isBreadcrumbVisible = true)
        {
            map.Add(
                new NavigationMapNode(key, content, parentKey: parentKey)
                {
                    Description = $"Groups the {content} pages in the gallery navigation hierarchy.",
                    IsBreadcrumbVisible = isBreadcrumbVisible
                }
            );
        }

        string AddPage<TPage>(object? content, string? parentKey = null, bool isBreadcrumbVisible = true)
        {
            string key = typeof(TPage).FullName
                      ?? throw new InvalidOperationException("A page route requires a full type name.");
            map.Add(
                new NavigationMapNode(
                    key,
                    content,
                    new NavigationTarget(typeof(TPage), key),
                    parentKey,
                    $"Opens the gallery page that demonstrates {content}.")
                {
                    IsBreadcrumbVisible = isBreadcrumbVisible,
                }
            );
            return key;
        }
    }
}
