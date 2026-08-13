// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.UnitTests.Appearance;

[Collection(nameof(ThemeManagerTestCollection))]
public class ThemeManagerTests
{
    private static readonly Color BuiltInAccent = Color.FromRgb(0x00, 0x78, 0xD4);
    private static readonly Uri LightThemeUri = new(
        "/Wpf.Zen.Ui;component/Resources/Theme/Light.xaml",
        UriKind.Relative
    );
    private static readonly Uri DarkThemeUri = new(
        "/Wpf.Zen.Ui;component/Resources/Theme/Dark.xaml",
        UriKind.Relative
    );

    [Fact]
    public void Initialize_DoesNotInstallGlobalThemeUntilExplicitConfiguration()
    {
        RunIsolated(() =>
        {
            Application.Current!.Resources = new ResourceDictionary();
            SetThemeManagementActivationState(0);

            ThemeManager.Initialize();

            Assert.Empty(Application.Current.Resources.MergedDictionaries);

            // The first explicit configuration must apply even though every value equals ThemeManager's defaults.
            ThemeManager.Configure(
                ThemePreference.Light,
                AccentColorSource.Default,
                BuiltInAccent,
                false,
                null,
                null,
                null,
                null
            );

            Assert.Same(
                GetThemeDictionary(ThemeVariant.Light),
                Assert.Single(Application.Current.Resources.MergedDictionaries)
            );
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));
        });
    }

    [Fact]
    public void ConfigureWithDefaultValues_ExplicitlyActivatesThemeManagement()
    {
        RunIsolated(() =>
        {
            Application.Current!.Resources = new ResourceDictionary();
            SetThemeManagementActivationState(0);

            // Calling ThemeManager.Configure is itself an explicit opt-in. The first call must therefore install
            // the global resources even when the supplied values are identical to the manager's current defaults.
            ThemeManager.Configure(ThemePreference.Light, AccentColorSource.Default);

            Assert.Same(
                GetThemeDictionary(ThemeVariant.Light),
                Assert.Single(Application.Current.Resources.MergedDictionaries)
            );
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));
        });
    }

    [Fact]
    public void EmptyThemeManagerOptions_ExplicitlyActivatesDefaultConfiguration()
    {
        RunIsolated(() =>
        {
            Application.Current!.Resources = new ResourceDictionary();
            SetThemeManagementActivationState(0);

            var options = new ThemeManagerOptions();
            options.BeginInit();
            options.EndInit();

            Assert.Same(
                GetThemeDictionary(ThemeVariant.Light),
                Assert.Single(Application.Current.Resources.MergedDictionaries)
            );
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));
        });
    }

    [Fact]
    public void InitialPreference_ReportsAndAppliesLightTheme()
    {
        RunIsolated(() =>
        {
            var resources = Application.Current!.Resources;

            Assert.Equal(ThemePreference.Light, ThemeManager.Preference);
            Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);
            Assert.Equal(AccentColorSource.Default, ThemeManager.AccentSource);
            Assert.Equal(BuiltInAccent, ThemeManager.CustomAccentColor);
            Assert.False(ThemeManager.EnableNativeControlStyling);
            Assert.Null(ThemeManager.AdditionalLightThemeUri);
            Assert.Null(ThemeManager.AdditionalDarkThemeUri);
            Assert.Null(ThemeManager.AdditionalHighContrastLightThemeUri);
            Assert.Null(ThemeManager.AdditionalHighContrastDarkThemeUri);
            Assert.Same(GetThemeDictionary(ThemeVariant.Light), resources.MergedDictionaries[0]);
            Assert.NotNull(resources[BrushKeys.WindowBackgroundBrush]);
        });
    }

    [Fact]
    public void Preference_RefreshesOncePerChangeAndPublishesEffectiveThemeAfterResources()
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
                ThemeManager.Preference = ThemePreference.Dark;
                ThemeManager.Preference = ThemePreference.Dark;
                ThemeManager.Preference = ThemePreference.Light;

                Assert.Collection(
                    notifications,
                    item => AssertNotification(item, ThemeVariant.Dark),
                    item => AssertNotification(item, ThemeVariant.Light)
                );
                Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);
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
            var invalidPreference = (ThemePreference)int.MaxValue;
            var invalidSource = (AccentColorSource)int.MaxValue;

            var preferenceException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Preference = invalidPreference
            );
            var sourceException = Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.AccentSource = invalidSource
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(invalidPreference, AccentColorSource.Default)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(ThemePreference.Light, invalidSource)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ThemeManager.Configure(
                    invalidPreference,
                    AccentColorSource.Default,
                    BuiltInAccent,
                    true,
                    null,
                    null,
                    null,
                    null
                )
            );

            Assert.Equal("preference", preferenceException.ParamName);
            Assert.Equal("source", sourceException.ParamName);
            Assert.Equal(ThemePreference.Light, ThemeManager.Preference);
            Assert.Equal(AccentColorSource.Default, ThemeManager.AccentSource);
            Assert.Equal(ThemeVariant.Light, ThemeManager.EffectiveTheme);
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

                var expected = AccentPalette.Create(opaque, ThemeVariant.Light);
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
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = true;
            Assert.True(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = false;
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = false;
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.EnableNativeControlStyling = true;
            Assert.True(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));
        });
    }

    [Fact]
    public void ConfigureWithoutNativeStyleArgument_PreservesExplicitOptInState()
    {
        RunIsolated(() =>
        {
            ThemeManager.Configure(
                ThemePreference.Light,
                AccentColorSource.Default,
                enableNativeControlStyling: true
            );

            ThemeManager.Configure(ThemePreference.Dark, AccentColorSource.System);

            Assert.True(ThemeManager.EnableNativeControlStyling);
            Assert.True(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));

            ThemeManager.Configure(
                ThemePreference.Light,
                AccentColorSource.Default,
                enableNativeControlStyling: false
            );
            ThemeManager.Configure(ThemePreference.Dark);

            Assert.False(ThemeManager.EnableNativeControlStyling);
            Assert.False(ContainsMergedSource("/Wpf.Zen.Ui;component/Resources/NativeControlStyles.xaml"));
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
                    ThemePreference.Dark,
                    AccentColorSource.Custom,
                    customAccent,
                    false,
                    DarkThemeUri,
                    LightThemeUri,
                    DarkThemeUri,
                    LightThemeUri
                );

                Assert.Equal(1, notificationCount);
                Assert.Equal(ThemePreference.Dark, ThemeManager.Preference);
                Assert.Equal(ThemeVariant.Dark, ThemeManager.EffectiveTheme);
                Assert.Equal(AccentColorSource.Custom, ThemeManager.AccentSource);
                Assert.Equal(Color.FromRgb(0xAA, 0x51, 0x0C), ThemeManager.CustomAccentColor);
                Assert.False(ThemeManager.EnableNativeControlStyling);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalDarkThemeUri);
                Assert.Equal(DarkThemeUri, ThemeManager.AdditionalHighContrastLightThemeUri);
                Assert.Equal(LightThemeUri, ThemeManager.AdditionalHighContrastDarkThemeUri);

                Assert.True(ContainsMergedSource(LightThemeUri.OriginalString));

                ThemeManager.Configure(
                    ThemePreference.Dark,
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
                    ThemePreference.Dark,
                    AccentColorSource.Custom,
                    Color.FromArgb(0x30, 0x22, 0x66, 0xAA),
                    false
                );

                Assert.Equal(1, notificationCount);
                Assert.Equal(ThemePreference.Dark, ThemeManager.Preference);
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
                    GetThemeDictionary(ThemeVariant.Dark).MergedDictionaries,
                    dictionary =>
                        dictionary.Source is null && dictionary.Contains(ColorKeys.AccentFillPrimary)
                );
                Assert.Equal(
                    AccentPalette.Create(Color.FromRgb(0xBB, 0x44, 0x11), ThemeVariant.Dark)
                        .AccentFillPrimary,
                    generatedAccentDictionary[ColorKeys.AccentFillPrimary]
                );

                ThemeManager.Configure(
                    ThemePreference.Dark,
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
    public void AdditionalThemeUris_AreAppendedToTheirMatchingPaletteOnly()
    {
        RunIsolated(() =>
        {
            ThemeManager.SetAdditionalThemeUris(
                DarkThemeUri,
                LightThemeUri,
                LightThemeUri,
                DarkThemeUri
            );

            var normalLight = new List<ResourceDictionary>();
            var normalDark = new List<ResourceDictionary>();
            ThemeManager.PopulateThemeDictionaries(
                normalLight,
                normalDark,
                highContrast: false,
                accentColor: BuiltInAccent,
                nativeControlStyles: null
            );

            Assert.Equal(DarkThemeUri, normalLight[^1].Source);
            Assert.Equal(LightThemeUri, normalDark[^1].Source);

            var highContrastLight = new List<ResourceDictionary>();
            var highContrastDark = new List<ResourceDictionary>();
            ThemeManager.PopulateThemeDictionaries(
                highContrastLight,
                highContrastDark,
                highContrast: true,
                accentColor: default,
                nativeControlStyles: null
            );

            Assert.Equal(LightThemeUri, highContrastLight[^1].Source);
            Assert.Equal(DarkThemeUri, highContrastDark[^1].Source);
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
            ThemeManager.Preference = ThemePreference.Dark;
            var resources = Application.Current!.Resources;
            var previouslyApplied = resources.MergedDictionaries[0];
            var missingDictionary = new Uri(
                "/Wpf.Zen.Ui;component/Resources/Theme/DefinitelyMissing.xaml",
                UriKind.Relative
            );

            var exception = Assert.Throws<InvalidOperationException>(() =>
                ThemeManager.AdditionalDarkThemeUri = missingDictionary
            );

            Assert.Contains(missingDictionary.OriginalString, exception.Message, StringComparison.Ordinal);
            Assert.Same(previouslyApplied, resources.MergedDictionaries[0]);
            Assert.Equal(ThemeVariant.Dark, ThemeManager.EffectiveTheme);

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

            Theme.SetRequested(element, ThemePreference.Dark);
            Assert.Same(GetThemeDictionary(ThemeVariant.Dark), element.Resources.MergedDictionaries[0]);
            Assert.Same(userDictionary, element.Resources.MergedDictionaries[1]);
            Assert.Equal("UserValue", element.Resources["UserKey"]);

            ThemeManager.Preference = ThemePreference.Light;
            Assert.Same(GetThemeDictionary(ThemeVariant.Dark), element.Resources.MergedDictionaries[0]);
            Assert.Single(
                element.Resources.MergedDictionaries,
                dictionary => ReferenceEquals(dictionary, GetThemeDictionary(ThemeVariant.Dark))
            );

            Theme.SetRequested(element, ThemePreference.Light);
            Assert.Same(GetThemeDictionary(ThemeVariant.Light), element.Resources.MergedDictionaries[0]);
            Assert.Equal(2, element.Resources.MergedDictionaries.Count);

            Theme.ClearRequested(element);
            Assert.Null(Theme.GetRequested(element));
            Assert.Same(DependencyProperty.UnsetValue, element.ReadLocalValue(Theme.RequestedProperty));
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

            Theme.SetRequested(run, ThemePreference.Dark);
            Assert.Same(GetThemeDictionary(ThemeVariant.Dark), run.Resources.MergedDictionaries[0]);

            ThemeManager.AccentSource = AccentColorSource.Custom;
            Assert.Same(GetThemeDictionary(ThemeVariant.Dark), run.Resources.MergedDictionaries[0]);

            Theme.SetRequested(run, null);
            Assert.Null(Theme.GetRequested(run));
            Assert.Same(DependencyProperty.UnsetValue, run.ReadLocalValue(Theme.RequestedProperty));
            Assert.Empty(run.Resources.MergedDictionaries);
        });
    }

    [Fact]
    public void RequestedTheme_RejectsUndefinedPreferenceWithoutInjectingResources()
    {
        RunIsolated(() =>
        {
            var element = new Border();

            Assert.Throws<ArgumentException>(() =>
                Theme.SetRequested(element, (ThemePreference)int.MaxValue)
            );

            Assert.Null(Theme.GetRequested(element));
            Assert.Empty(element.Resources.MergedDictionaries);
        });
    }

    [Fact]
    public void RequestedTheme_XamlAcceptsConcretePreferenceForNullableProperty()
    {
        RunIsolated(() =>
        {
            const string xaml = """
                <Border
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                    ui:Theme.Requested="Dark" />
                """;

            var element = Assert.IsType<Border>(XamlReader.Parse(xaml));

            Assert.Equal(ThemePreference.Dark, Theme.GetRequested(element));
            Assert.Same(GetThemeDictionary(ThemeVariant.Dark), element.Resources.MergedDictionaries[0]);
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
                Theme.SetRequested(element, ThemePreference.Dark);
                Assert.Same(GetThemeDictionary(ThemeVariant.Dark), element.Resources.MergedDictionaries[0]);

                // The owner, rather than ThemeManager, still owns the matching EndInit call.
                void BeginNestedTransaction() => element.Resources.BeginInit();
                Assert.Throws<InvalidOperationException>(BeginNestedTransaction);
            }
            finally
            {
                element.Resources.EndInit();
                Theme.ClearRequested(element);
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
            ThemeManager.Preference = ThemePreference.Dark;
            ThemeManager.Changed -= handler;
            ThemeManager.Preference = ThemePreference.Light;

            Assert.Equal(1, notificationCount);
        });
    }

    [Fact]
    public void SystemPreference_ResolvesToAConcreteTheme()
    {
        RunIsolated(() =>
        {
            ThemeManager.Preference = ThemePreference.System;

            Assert.Contains(
                ThemeManager.EffectiveTheme,
                new[] { ThemeVariant.Light, ThemeVariant.Dark }
            );
            Assert.Same(
                GetThemeDictionary(ThemeManager.EffectiveTheme),
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
            ThemePreference.Dark,
            AccentColorSource.Default,
            BuiltInAccent,
            false,
            null,
            null,
            null,
            null
        );
        ThemeManager.Configure(
            ThemePreference.Light,
            AccentColorSource.Default,
            BuiltInAccent,
            false,
            null,
            null,
            null,
            null
        );
    }

    private static ResourceDictionary GetThemeDictionary(ThemeVariant variant)
    {
        var fieldName = variant == ThemeVariant.Dark ? "_darkDictionary" : "_lightDictionary";
        var field = typeof(ThemeManager).GetField(
            fieldName,
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );

        return Assert.IsType<ResourceDictionary>(field?.GetValue(null));
    }

    private static void SetThemeManagementActivationState(int value)
    {
        var field = typeof(ThemeManager).GetField(
            "_themeManagementActivationState",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        );
        Assert.NotNull(field);
        field.SetValue(null, value);
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
        ThemeVariant expectedTheme
    )
    {
        Assert.IsType<ThemeManager>(notification.Sender);
        Assert.Equal(expectedTheme, notification.Args.EffectiveTheme);
        Assert.Equal(ThemeChangeSource.Manual, notification.Args.ChangeSource);
        Assert.NotNull(notification.Resource);
    }
}
