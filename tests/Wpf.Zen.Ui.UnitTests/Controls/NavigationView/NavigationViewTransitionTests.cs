// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Wpf.Zen.Ui.Animations;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewTransitionTests
{
    [Fact]
    public void PageTransitionProperties_UseIndependentPageLocalDeclarations()
    {
        WpfTestThread.Run(() =>
        {
            var defaultPage = new Page();
            var customPage = new Page();

            Wpf.Zen.Ui.Controls.NavigationView.SetTransition(customPage, Transition.SlideLeft);
            Wpf.Zen.Ui.Controls.NavigationView.SetTransitionDuration(customPage, 650);

            Assert.Equal(
                Transition.FadeInWithSlide,
                Wpf.Zen.Ui.Controls.NavigationView.GetTransition(defaultPage)
            );
            Assert.Equal(
                200,
                Wpf.Zen.Ui.Controls.NavigationView.GetTransitionDuration(defaultPage)
            );
            Assert.Equal(
                Transition.SlideLeft,
                Wpf.Zen.Ui.Controls.NavigationView.GetTransition(customPage)
            );
            Assert.Equal(
                650,
                Wpf.Zen.Ui.Controls.NavigationView.GetTransitionDuration(customPage)
            );

            Assert.Equal(
                DependencyProperty.UnsetValue,
                defaultPage.ReadLocalValue(Wpf.Zen.Ui.Controls.NavigationView.TransitionProperty)
            );
            Assert.Equal(
                Transition.SlideLeft,
                customPage.ReadLocalValue(Wpf.Zen.Ui.Controls.NavigationView.TransitionProperty)
            );
        });
    }

    [Fact]
    public void TransitionProperties_SupportGlobalAndAttachedContracts()
    {
        Type navigationViewType = typeof(Wpf.Zen.Ui.Controls.NavigationView);

        Assert.NotNull(
            navigationViewType.GetProperty(
                "Transition",
                BindingFlags.Public | BindingFlags.Instance
            )
        );
        Assert.NotNull(
            navigationViewType.GetProperty(
                "TransitionDuration",
                BindingFlags.Public | BindingFlags.Instance
            )
        );
        Assert.NotNull(
            navigationViewType.GetMethod(
                nameof(Wpf.Zen.Ui.Controls.NavigationView.GetTransition),
                BindingFlags.Public | BindingFlags.Static
            )
        );
        Assert.NotNull(
            navigationViewType.GetMethod(
                nameof(Wpf.Zen.Ui.Controls.NavigationView.GetTransitionDuration),
                BindingFlags.Public | BindingFlags.Static
            )
        );
    }

    [Fact]
    public void NavigationTransitionSettings_PageDeclarationsOverrideGlobalSettingsIndependently()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Transition = Transition.SlideRight,
                TransitionDuration = 475
            };
            var page = new Page();
            Wpf.Zen.Ui.Controls.NavigationView.SetTransition(page, Transition.None);

            Wpf.Zen.Ui.Controls.NavigationView.NavigationTransitionSettings settings =
                navigationView.GetNavigationTransitionSettings(page);

            Assert.Equal(Transition.None, settings.Transition);
            Assert.Equal(475, settings.Duration);

            Wpf.Zen.Ui.Controls.NavigationView.SetTransitionDuration(page, 0);
            settings = navigationView.GetNavigationTransitionSettings(page);

            Assert.Equal(Transition.None, settings.Transition);
            Assert.Equal(0, settings.Duration);
        });
    }

    [Fact]
    public void NavigationTransitionSettings_PageWithoutDeclarationsUsesGlobalSettings()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Transition = Transition.SlideLeft,
                TransitionDuration = 625
            };

            Wpf.Zen.Ui.Controls.NavigationView.NavigationTransitionSettings settings =
                navigationView.GetNavigationTransitionSettings(new Page());

            Assert.Equal(Transition.SlideLeft, settings.Transition);
            Assert.Equal(625, settings.Duration);
        });
    }

    [Fact]
    public void NavigationTransitionSettings_ExplicitDefaultValueStillOverridesGlobalSetting()
    {
        WpfTestThread.Run(() =>
        {
            var navigationView = new Wpf.Zen.Ui.Controls.NavigationView
            {
                Transition = Transition.SlideLeft,
                TransitionDuration = 625
            };
            var page = new Page();
            Wpf.Zen.Ui.Controls.NavigationView.SetTransition(
                page,
                Transition.FadeInWithSlide
            );
            Wpf.Zen.Ui.Controls.NavigationView.SetTransitionDuration(page, 200);

            Wpf.Zen.Ui.Controls.NavigationView.NavigationTransitionSettings settings =
                navigationView.GetNavigationTransitionSettings(page);

            Assert.Equal(Transition.FadeInWithSlide, settings.Transition);
            Assert.Equal(200, settings.Duration);
        });
    }

    [Fact]
    public void TransitionProperty_RejectsUndefinedValues()
    {
        WpfTestThread.Run(() =>
        {
            var page = new Page();

            Assert.Throws<ArgumentException>(() =>
                Wpf.Zen.Ui.Controls.NavigationView.SetTransition(page, (Transition)int.MaxValue)
            );
        });
    }
}
