// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Theming;

namespace Wpf.Ui.UnitTests.Appearance;

[Collection(nameof(ThemeManagerTestCollection))]
public class ThemeManagerTests
{
    private static readonly Color BuiltInAccent = Color.FromRgb(0x00, 0x78, 0xD4);
    private static readonly Uri LightThemeUri = new(
        "/Wpf.Ui;component/Resources/Theme/Light.xaml",
        UriKind.Relative
    );
    private static readonly Uri DarkThemeUri = new(
        "/Wpf.Ui;component/Resources/Theme/Dark.xaml",
        UriKind.Relative
    );

    [Fact]
    public void DefaultMode_AppliesLightCompatibleResourcesButReportsDefault()
    {
        RunIsolated(() =>
        {
            var resources = Application.Current!.Resources;

            Assert.Equal(UiThemeMode.Default, ThemeManager.ThemeMode);
            Assert.Equal(ApplicationTheme.Default, ThemeManager.CurrentTheme);
            Assert.Equal(AccentColorSource.Default, ThemeManager.AccentSource);
            Assert.Equal(BuiltInAccent, ThemeManager.CustomAccentColor);
            Assert.True(ThemeManager.EnableNativeControlStyling);
            Assert.Null(ThemeManager.AdditionalLightThemeUri);
            Assert.Null(ThemeManager.AdditionalDarkThemeUri);
            Assert.Null(ThemeManager.AdditionalHighContrastLightThemeUri);
            Assert.Null(ThemeManager.AdditionalHighContrastDarkThemeUri);
            Assert.Same(GetThemeDictionary(UiThemeMode.Light), resources.MergedDictionaries[0]);
            Assert.NotNull(resources[BrushKeys.WindowBackgroundBrush]);
        });
    }

    [Fact]
    public void ThemeMode_RefreshesOncePerChangeAndPublishesAppliedThemeAfterResources()
    {
        RunIsolated(() =>
        {
            var notifications = new List<(object? Sender, ThemeChangedEventArgs Args, object? Resource)>();
            EventHandler<ThemeChangedEventArgs> handler = (sender, args) =>
                notifications.Add(
                    (sender, args, Application.Current!.TryFindResource(BrushKeys.WindowBackgroundBrush))
                );
            ThemeManager.Changed += handler;

            try
            {
                ThemeManager.ThemeMode = UiThemeMode.Dark;
                ThemeManager.ThemeMode = UiThemeMode.Dark;
                ThemeManager.ThemeMode = UiThemeMode.Light;
                ThemeManager.ThemeMode = UiThemeMode.Default;

                Assert.Collection(
                    notifications,
                    item => AssertNotification(item, ApplicationTheme.Dark),
                    item => AssertNotification(item, ApplicationTheme.Light),
                    item => AssertNotification(item, ApplicationTheme.Default)
                );
                Assert.Equal(ApplicationTheme.Default, ThemeManager.CurrentTheme);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void InvalidEnums_AreRejectedWithoutMutatingConfiguration()
    {
        RunIsolated(() =>
        {
            var invalidMode = (UiThemeMode)int.MaxValue;
            var invalidSource = (AccentColorSource)int.MaxValue;

            var modeException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.ThemeMode = invalidMode
            );
            var sourceException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.AccentSource = invalidSource
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(invalidMode, AccentColorSource.Default)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(UiThemeMode.Light, invalidSource)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(
                    invalidMode,
                    AccentColorSource.Default,
                    BuiltInAccent,
                    true,
                    null,
                    null,
                    null,
                    null
                )
            );

            Assert.Equal("mode", modeException.ParamName);
            Assert.Equal("source", sourceException.ParamName);
            Assert.Equal(UiThemeMode.Default, ThemeManager.ThemeMode);
            Assert.Equal(AccentColorSource.Default, ThemeManager.AccentSource);
            Assert.Equal(ApplicationTheme.Default, ThemeManager.CurrentTheme);
        });
    }

    [Fact]
    public void CustomAccent_NormalizesAlphaAndBuildsMatchingFrozenColorAndBrushResources()
    {
        RunIsolated(() =>
        {
            var translucent = Color.FromArgb(0x24, 0x31, 0x72, 0xB8);
            var opaque = Color.FromRgb(0x31, 0x72, 0xB8);
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;
            ThemeManager.Changed += handler;

            try
            {
                // The remembered custom color is inert while another source is selected.
                ThemeManager.CustomAccentColor = translucent;
                Assert.Equal(opaque, ThemeManager.CustomAccentColor);
                Assert.Equal(0, notificationCount);

                ThemeManager.AccentSource = AccentColorSource.Custom;
                Assert.Equal(1, notificationCount);

                var expected = AccentPalette.Create(opaque, UiThemeMode.Light);
                var resources = Application.Current!.Resources;
                Assert.Equal(expected.AccentFillPrimary, resources[ColorKeys.AccentFillPrimary]);
                Assert.Equal(expected.AccentTextPrimary, resources[ColorKeys.SystemFillColorAttention]);

                var brush = Assert.IsType<SolidColorBrush>(resources[BrushKeys.AccentFillPrimaryBrush]);
                Assert.Equal(expected.AccentFillPrimary, brush.Color);
                Assert.True(brush.IsFrozen);

                var attentionBrush = Assert.IsType<SolidColorBrush>(
                    resources[BrushKeys.SystemFillColorAttentionBrush]
                );
                Assert.Equal(expected.AccentTextPrimary, attentionBrush.Color);
                Assert.True(attentionBrush.IsFrozen);

                ThemeManager.CustomAccentColor = Color.FromArgb(0, 0x31, 0x72, 0xB8);
                Assert.Equal(1, notificationCount);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void EnableNativeControlStyling_AddsAndRemovesNativeStyleDictionary()
    {
        RunIsolated(() =>
        {
            Assert.True(ContainsMergedSource("/Wpf.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = false;
            Assert.False(ContainsMergedSource("/Wpf.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = false;
            Assert.False(ContainsMergedSource("/Wpf.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = true;
            Assert.True(ContainsMergedSource("/Wpf.Ui;component/Resources/NativeControlStyles.xaml"));
        });
    }

    [Fact]
    public void ConfigureFullOverload_AppliesAllSettingsInOneRefresh()
    {
        RunIsolated(() =>
        {
            var customAccent = Color.FromArgb(0x40, 0xAA, 0x51, 0x0C);
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;
            ThemeManager.Changed += handler;

            try
            {
                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Custom,
                    customAccent,
                    false,
                    DarkThemeUri,
                    LightThemeUri,
                    DarkThemeUri,
                    LightThemeUri
                );

                Assert.Equal(1, notificationCount);
                Assert.Equal(UiThemeMode.Dark, ThemeManager.ThemeMode);
                Assert.Equal(ApplicationTheme.Dark, ThemeManager.CurrentTheme);
                Assert.Equal(AccentColorSource.Custom, ThemeManager.AccentSource);
                Assert.Equal(Color.FromRgb(0xAA, 0x51, 0x0C), ThemeManager.CustomAccentColor);
                Assert.False(ThemeManager.EnableNativeControlStyling);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalDarkThemeUri);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalHighContrastLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalHighContrastDarkThemeUri);

                Assert.True(ContainsMergedSource(LightThemeUri.OriginalString));

                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Custom,
                    customAccent,
                    false,
                    DarkThemeUri,
                    LightThemeUri,
                    DarkThemeUri,
                    LightThemeUri
                );
                Assert.Equal(1, notificationCount);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void ConfigureShortOverload_PreservesAdditionalUrisAndRefreshesCustomAccentChanges()
    {
        RunIsolated(() =>
        {
            ThemeManager.SetAdditionalThemeUris(
                DarkThemeUri,
                LightThemeUri,
                LightThemeUri,
                DarkThemeUri
            );
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;
            ThemeManager.Changed += handler;

            try
            {
                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Custom,
                    Color.FromArgb(0x30, 0x22, 0x66, 0xAA),
                    false
                );

                Assert.Equal(1, notificationCount);
                Assert.Equal(UiThemeMode.Dark, ThemeManager.ThemeMode);
                Assert.Equal(AccentColorSource.Custom, ThemeManager.AccentSource);
                Assert.Equal(Color.FromRgb(0x22, 0x66, 0xAA), ThemeManager.CustomAccentColor);
                Assert.False(ThemeManager.EnableNativeControlStyling);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalDarkThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalHighContrastLightThemeUri);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalHighContrastDarkThemeUri);

                ThemeManager.CustomAccentColor = Color.FromRgb(0xBB, 0x44, 0x11);
                Assert.Equal(2, notificationCount);
                var generatedAccentDictionary = Assert.Single(
                    GetThemeDictionary(UiThemeMode.Dark).MergedDictionaries,
                    dictionary =>
                        dictionary.Source is null && dictionary.Contains(ColorKeys.AccentFillPrimary)
                );
                Assert.Equal(
                    AccentPalette.Create(Color.FromRgb(0xBB, 0x44, 0x11), UiThemeMode.Dark)
                        .AccentFillPrimary,
                    generatedAccentDictionary[ColorKeys.AccentFillPrimary]
                );

                ThemeManager.Configure(
                    UiThemeMode.Dark,
                    AccentColorSource.Custom,
                    Color.FromRgb(0xBB, 0x44, 0x11),
                    false
                );
                Assert.Equal(2, notificationCount);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void SetAdditionalThemeUris_StoresAllUrisAndRefreshesOnlyOnce()
    {
        RunIsolated(() =>
        {
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;
            ThemeManager.Changed += handler;

            try
            {
                ThemeManager.SetAdditionalThemeUris(
                    DarkThemeUri,
                    LightThemeUri,
                    LightThemeUri,
                    DarkThemeUri
                );

                Assert.Equal(1, notificationCount);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalDarkThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalHighContrastLightThemeUri);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalHighContrastDarkThemeUri);

                ThemeManager.SetAdditionalThemeUris(
                    DarkThemeUri,
                    LightThemeUri,
                    LightThemeUri,
                    DarkThemeUri
                );
                Assert.Equal(1, notificationCount);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void AdditionalUriProperties_RefreshIndependentlyAndIgnoreEqualAssignments()
    {
        RunIsolated(() =>
        {
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;
            ThemeManager.Changed += handler;

            try
            {
                ThemeManager.AdditionalLightThemeUri = DarkThemeUri;
                ThemeManager.AdditionalDarkThemeUri = LightThemeUri;
                ThemeManager.AdditionalHighContrastLightThemeUri = LightThemeUri;
                ThemeManager.AdditionalHighContrastDarkThemeUri = DarkThemeUri;
                Assert.Equal(4, notificationCount);

                ThemeManager.AdditionalLightThemeUri = DarkThemeUri;
                ThemeManager.AdditionalDarkThemeUri = LightThemeUri;
                ThemeManager.AdditionalHighContrastLightThemeUri = LightThemeUri;
                ThemeManager.AdditionalHighContrastDarkThemeUri = DarkThemeUri;
                Assert.Equal(4, notificationCount);
            }
            finally
            {
                ThemeManager.Changed -= handler;
            }
        });
    }

    [Fact]
    public void InvalidAdditionalDictionary_LeavesPreviouslyAppliedResourcesUntouched()
    {
        RunIsolated(() =>
        {
            ThemeManager.ThemeMode = UiThemeMode.Dark;
            var resources = Application.Current!.Resources;
            var previouslyApplied = resources.MergedDictionaries[0];
            var missingDictionary = new Uri(
                "/Wpf.Ui;component/Resources/Theme/DefinitelyMissing.xaml",
                UriKind.Relative
            );

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ThemeManager.AdditionalDarkThemeUri = missingDictionary
            );

            Assert.Contains(missingDictionary.OriginalString, exception.Message, StringComparison.Ordinal);
            Assert.Same(previouslyApplied, resources.MergedDictionaries[0]);
            Assert.Equal(ApplicationTheme.Dark, ThemeManager.CurrentTheme);

            // A failed refresh records the requested URI, so explicitly clear it before test cleanup.
            ThemeManager.SetAdditionalThemeUris(null, null, null, null);
        });
    }

    [Fact]
    public void RequestedTheme_OnFrameworkElementSwitchesScopeAndPreservesUserResources()
    {
        RunIsolated(() =>
        {
            var element = new Border();
            var userDictionary = new ResourceDictionary { ["UserKey"] = "UserValue" };
            element.Resources.MergedDictionaries.Add(userDictionary);

            Theme.SetRequested(element, UiThemeMode.Dark);
            Assert.Same(GetThemeDictionary(UiThemeMode.Dark), element.Resources.MergedDictionaries[0]);
            Assert.Same(userDictionary, element.Resources.MergedDictionaries[1]);
            Assert.Equal("UserValue", element.Resources["UserKey"]);

            ThemeManager.ThemeMode = UiThemeMode.Light;
            Assert.Same(GetThemeDictionary(UiThemeMode.Dark), element.Resources.MergedDictionaries[0]);
            Assert.Single(
                element.Resources.MergedDictionaries,
                dictionary => ReferenceEquals(dictionary, GetThemeDictionary(UiThemeMode.Dark))
            );

            Theme.SetRequested(element, UiThemeMode.Light);
            Assert.Same(GetThemeDictionary(UiThemeMode.Light), element.Resources.MergedDictionaries[0]);
            Assert.Equal(2, element.Resources.MergedDictionaries.Count);

            Theme.SetRequested(element, UiThemeMode.Default);
            Assert.Single(element.Resources.MergedDictionaries);
            Assert.Same(userDictionary, element.Resources.MergedDictionaries[0]);
        });
    }

    [Fact]
    public void RequestedTheme_OnFrameworkContentElementTracksGlobalRefreshAndCanBeRemoved()
    {
        RunIsolated(() =>
        {
            var run = new Run("Theme scope");

            Theme.SetRequested(run, UiThemeMode.Dark);
            Assert.Same(GetThemeDictionary(UiThemeMode.Dark), run.Resources.MergedDictionaries[0]);

            ThemeManager.AccentSource = AccentColorSource.Custom;
            Assert.Same(GetThemeDictionary(UiThemeMode.Dark), run.Resources.MergedDictionaries[0]);

            Theme.SetRequested(run, UiThemeMode.Default);
            Assert.Empty(run.Resources.MergedDictionaries);
        });
    }

    [Fact]
    public void RequestedTheme_DuringResourceInitializationDoesNotEndOwnersTransaction()
    {
        RunIsolated(() =>
        {
            var element = new Border();
            element.Resources.BeginInit();

            try
            {
                Theme.SetRequested(element, UiThemeMode.Dark);
                Assert.Same(GetThemeDictionary(UiThemeMode.Dark), element.Resources.MergedDictionaries[0]);

                // The owner, rather than ThemeManager, still owns the matching EndInit call.
                void BeginNestedTransaction() => element.Resources.BeginInit();
                Assert.Throws<InvalidOperationException>(BeginNestedTransaction);
            }
            finally
            {
                element.Resources.EndInit();
                Theme.SetRequested(element, UiThemeMode.Default);
            }
        });
    }

    [Fact]
    public void ApplyToRootVisual_CanBeEnabledRaisedAndRemovedBeforePresentationSourceExists()
    {
        RunIsolated(() =>
        {
            var element = new Border();

            Theme.SetApplyToRootVisual(element, true);
            Assert.True(Theme.GetApplyToRootVisual(element));
            Assert.Empty(element.Resources.MergedDictionaries);

            // Raising Loaded without an HwndSource exercises the deferred registration path. The manager must
            // tolerate hosts that load content before connecting it to a native presentation source.
            element.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent, element));
            Assert.Empty(element.Resources.MergedDictionaries);

            Theme.SetApplyToRootVisual(element, false);
            Assert.False(Theme.GetApplyToRootVisual(element));
        });
    }

    [Fact]
    public void Changed_RemoveStopsFurtherNotifications()
    {
        RunIsolated(() =>
        {
            var notificationCount = 0;
            EventHandler<ThemeChangedEventArgs> handler = (_, _) => notificationCount++;

            ThemeManager.Changed += handler;
            ThemeManager.ThemeMode = UiThemeMode.Dark;
            ThemeManager.Changed -= handler;
            ThemeManager.ThemeMode = UiThemeMode.Light;

            Assert.Equal(1, notificationCount);
        });
    }

    [Fact]
    public void SystemMode_ResolvesToAConcreteTheme()
    {
        RunIsolated(() =>
        {
            ThemeManager.ThemeMode = UiThemeMode.System;

            Assert.Contains(
                ThemeManager.CurrentTheme,
                new[] { ApplicationTheme.Light, ApplicationTheme.Dark }
            );
            Assert.Same(
                GetThemeDictionary(
                    ThemeManager.CurrentTheme == ApplicationTheme.Dark
                        ? UiThemeMode.Dark
                        : UiThemeMode.Light
                ),
                Application.Current!.Resources.MergedDictionaries[0]
            );
        });
    }

    [Fact]
    public void HighContrastDark_UsesTheDocumentedSystemColorLuminanceComparison()
    {
        RunIsolated(() =>
        {
            var background = SystemColors.WindowColor;
            var foreground = SystemColors.WindowTextColor;
            var backgroundLuma =
                (0.299 * background.R) + (0.587 * background.G) + (0.114 * background.B);
            var foregroundLuma =
                (0.299 * foreground.R) + (0.587 * foreground.G) + (0.114 * foreground.B);

            Assert.Equal(backgroundLuma < foregroundLuma, ThemeManager.IsHighContrastDark());
        });
    }

    private static void RunIsolated(Action action)
    {
        WpfApplicationTestHost.Run(() =>
        {
            ResetThemeManager();

            try
            {
                action();
            }
            finally
            {
                ResetThemeManager();
            }
        });
    }

    private static void ResetThemeManager()
    {
        ThemeManager.Initialize();
        Application.Current!.Resources = new ResourceDictionary();

        // Move through a different state first so Configure cannot short-circuit after a preceding test happened
        // to leave the static manager at the desired defaults.
        ThemeManager.Configure(
            UiThemeMode.Dark,
            AccentColorSource.Default,
            BuiltInAccent,
            false,
            null,
            null,
            null,
            null
        );
        ThemeManager.Configure(
            UiThemeMode.Default,
            AccentColorSource.Default,
            BuiltInAccent,
            true,
            null,
            null,
            null,
            null
        );
    }

    private static ResourceDictionary GetThemeDictionary(UiThemeMode mode)
    {
        var fieldName = mode == UiThemeMode.Dark ? "_darkDictionary" : "_lightDictionary";
        var field = typeof(ThemeManager).GetField(
            fieldName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );

        return Assert.IsType<ResourceDictionary>(field?.GetValue(null));
    }

    private static bool ContainsMergedSource(string source)
    {
        var activeDictionary = Application.Current!.Resources.MergedDictionaries[0];
        return activeDictionary.MergedDictionaries.Any(dictionary =>
            string.Equals(dictionary.Source?.OriginalString, source, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static void AssertNotification(
        (object? Sender, ThemeChangedEventArgs Args, object? Resource) notification,
        ApplicationTheme expectedTheme
    )
    {
        Assert.IsType<ThemeManager>(notification.Sender);
        Assert.Equal(expectedTheme, notification.Args.AppliedTheme);
        Assert.Equal(ThemeChangeSource.Manual, notification.Args.ChangeSource);
        Assert.NotNull(notification.Resource);
    }
}