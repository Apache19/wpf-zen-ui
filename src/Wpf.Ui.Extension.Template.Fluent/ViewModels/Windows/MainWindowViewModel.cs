using System.Collections.ObjectModel;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace $safeprojectname$.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "WPF UI - $safeprojectname$";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationItem()
            {
                Content = "Home",
                IconSource = new SymbolIconSource { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationItem()
            {
                Content = "Data",
                IconSource = new SymbolIconSource { Symbol = SymbolRegular.DataHistogram24 },
                TargetPageType = typeof(Views.Pages.DataPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationItem()
            {
                Content = "Settings",
                IconSource = new SymbolIconSource { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };
    }
}
