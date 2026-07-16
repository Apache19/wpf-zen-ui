// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewItemTests
{
    private static readonly DependencyProperty NavigationParentProperty =
        typeof(Wpf.Ui.Controls.NavigationView)
            .GetField("NavigationParentProperty", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null) as DependencyProperty
        ?? throw new InvalidOperationException("NavigationParentProperty was not found.");

    private static readonly MethodInfo RemoveAccessKeyMarkersMethod = typeof(NavigationViewItem).GetMethod(
        "RemoveAccessKeyMarkers",
        BindingFlags.NonPublic | BindingFlags.Static
    )!;

    [Fact]
    public void PaneState_IsReadOnlyAndSynchronizesFromHostThroughTheItemTree()
    {
        RunOnStaThread(() =>
        {
            var navigationView = new Wpf.Ui.Controls.NavigationView
            {
                PaneDisplayMode = NavigationViewPaneDisplayMode.Top,
                IsPaneOpen = true,
            };
            var parent = new NavigationViewItem();
            var child = new NavigationViewItem();

            parent.MenuItems.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.True(NavigationViewItem.PaneDisplayModeProperty.ReadOnly);
            Assert.True(NavigationViewItem.IsSubItemProperty.ReadOnly);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, parent.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Top, child.PaneDisplayMode);
            Assert.True(child.IsSubItem);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;
            navigationView.IsPaneOpen = false;

            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, parent.PaneDisplayMode);
            Assert.Equal(NavigationViewPaneDisplayMode.Bottom, child.PaneDisplayMode);
            Assert.False(parent.IsPaneOpen);
            Assert.False(child.IsPaneOpen);

            parent.MenuItems.Remove(child);

            Assert.False(child.IsSubItem);
            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void ItemStyle_SelectsTopLevelAndSubItemTemplatesFromPaneDisplayMode()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var parent = new NavigationViewItem { Style = itemStyle };
            var child = new NavigationViewItem { Style = itemStyle };

            parent.MenuItems.Add(child);
            AttachToNavigationView(parent, navigationView);

            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftCompactTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftMinimal;

            Assert.Same(resources["NavigationViewItemLeftMinimalCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemLeftMinimalSubTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Top;

            Assert.Same(resources["NavigationViewItemTopCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemTopSubTemplate"], child.Template);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.Bottom;

            Assert.Same(resources["NavigationViewItemBottomCompactTemplate"], parent.Template);
            Assert.Same(resources["NavigationViewItemBottomCompactTemplate"], child.Template);

            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Fact]
    public void TemplateBehavior_HandlesChevronSelectionAndFluentIconState()
    {
        RunOnStaThread(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var navigationView = new Wpf.Ui.Controls.NavigationView();
            var icon = new SymbolIcon(SymbolRegular.Home24);
            var parent = new NavigationViewItem { Icon = icon, Style = itemStyle };
            var child = new NavigationViewItem { Style = itemStyle };

            parent.MenuItems.Add(child);
            AttachToNavigationView(parent, navigationView);
            child.Activate(navigationView);
            Assert.True(parent.ApplyTemplate());

            var expander = Assert.IsType<NavigationViewItemExpander>(
                parent.Template.FindName("PART_ChevronButton", parent)
            );
            Assert.Same(parent, expander.TemplatedParent);
            Assert.True(child.IsActive);
            Assert.Same(
                navigationView,
                typeof(NavigationViewItem)
                    .GetProperty("NavigationViewHost", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(parent)
            );

            parent.Activate(navigationView);
            parent.IsExpanded = false;
            InvokeClick(expander);
            Assert.True(parent.IsExpanded);
            Assert.False(parent.IsActive);

            InvokeClick(expander);
            Assert.False(parent.IsExpanded);
            Assert.True(parent.IsActive);

            navigationView.PaneDisplayMode = NavigationViewPaneDisplayMode.LeftFluent;
            Assert.True(parent.ApplyTemplate());
            Assert.True(icon.Filled);

            parent.Deactivate(navigationView);
            Assert.False(icon.Filled);

            parent.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }

    [Theory]
    [InlineData("Date & Time")]
    [InlineData("Dialogs & flyouts")]
    [InlineData("File _ name")]
    [InlineData("Trailing &")]
    [InlineData("Punctuation &!")]
    public void RemoveAccessKeyMarkers_PreservesCharactersThatDoNotPrefixAccessKeys(string value)
    {
        Assert.Equal(value, RemoveAccessKeyMarkers(value));
    }

    [Theory]
    [InlineData("_File", "File")]
    [InlineData("E_xit", "Exit")]
    [InlineData("&Settings", "Settings")]
    [InlineData("文件(_F)", "文件F")]
    [InlineData("Save && Close", "Save & Close")]
    [InlineData("Save __ Close", "Save _ Close")]
    public void RemoveAccessKeyMarkers_RemovesValidMarkersAndUnescapesLiteralMarkers(
        string value,
        string expected
    )
    {
        Assert.Equal(expected, RemoveAccessKeyMarkers(value));
    }

    private static string RemoveAccessKeyMarkers(string value) =>
        (string)RemoveAccessKeyMarkersMethod.Invoke(null, [value])!;

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }

    private static void InvokeClick(NavigationViewItemExpander toggleButton)
    {
        typeof(System.Windows.Controls.Primitives.ButtonBase)
            .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(toggleButton, null);
    }

    private static void AttachToNavigationView(
        NavigationViewItem item,
        Wpf.Ui.Controls.NavigationView navigationView
    )
    {
        item.SetValue(NavigationParentProperty, navigationView);
        item.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
