// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Services;

public class WindowsProviderService
{
    private readonly IServiceProvider _serviceProvider;

    public WindowsProviderService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Show<T>()
        where T : class
    {
        if (!typeof(Window).IsAssignableFrom(typeof(T)))
        {
            throw new InvalidOperationException($"The window class should be derived from {typeof(Window)}.");
        }

        // Windows are UI views and are intentionally not registered in the container, matching the
        // page activation rules of AddNavigation. GetServiceOrCreateInstance activates the window
        // and injects its registered constructor dependencies, such as the view model, while an
        // explicit registration (custom factory or lifetime) is still honored when present.
        Window windowInstance = (Window)ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, typeof(T));
        windowInstance.Owner = Application.Current.MainWindow;
        windowInstance.Show();
    }
}
