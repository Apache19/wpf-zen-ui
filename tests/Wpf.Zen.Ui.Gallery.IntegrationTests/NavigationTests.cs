// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;

namespace Wpf.Zen.Ui.Gallery.IntegrationTests;

public sealed class NavigationTests : UiTest
{
    [Fact]
    public async Task NavigationMapPage_ShouldDisplayTheLiveGalleryMap()
    {
        await Wait(2, TestContext.Current.CancellationToken);

        AutomationElement? navigationSection = FindFirst(c => c.ByText("Navigation"));
        navigationSection
            .Should()
            .NotBeNull("because the Navigation section should be present in the main menu");
        navigationSection.Click();

        await Wait(1, TestContext.Current.CancellationToken);

        AutomationElement? navigationMapItem = FindFirst(c => c.ByText("Navigation map"));
        if (navigationMapItem is null)
        {
            // Pane state is persisted by the control during a test process. Reopen the section if
            // the first activation collapsed an already expanded group.
            navigationSection.Click();
            await Wait(1, TestContext.Current.CancellationToken);
            navigationMapItem = FindFirst(c => c.ByText("Navigation map"));
        }

        navigationMapItem
            .Should()
            .NotBeNull("because the Navigation map sample should be registered under Navigation");
        navigationMapItem.Click();

        await Wait(1, TestContext.Current.CancellationToken);

        AutomationElement? map = FindFirst("GalleryNavigationMap");
        map.Should().NotBeNull("because the sample page should expose its live NavigationMapView");
        AutomationElement? homeNode = map.FindFirstDescendant(c => c.ByText("Home"));
        homeNode
            .Should()
            .NotBeNull("because the view should generate nodes from the gallery host's real map");
        homeNode.Click();

        await Wait(1, TestContext.Current.CancellationToken);

        FindFirst(c => c.ByText("Fluent UI"))
            .Should()
            .NotBeNull("because invoking a target node should navigate through the active host scope");

        // Reopen the sample after validating node invocation. Direction changes intentionally
        // preserve ScrollViewer offsets, so exercising navigation first keeps the two independent
        // behaviors from making one another dependent on physical viewport position.
        navigationMapItem.Click();
        await Wait(1, TestContext.Current.CancellationToken);

        map = FindFirst("GalleryNavigationMap");
        map.Should().NotBeNull("because the navigation map sample should reopen");
        ComboBox? directionPicker = FindFirst("NavigationMapDirectionPicker")?.AsComboBox();
        directionPicker
            .Should()
            .NotBeNull("because the sample should allow changing the live map direction");

        directionPicker.Select("Left to right");
        await Wait(1, TestContext.Current.CancellationToken);
        map.ItemStatus
            .Should()
            .Be(
                "LeftToRight",
                "because selecting the horizontal option should update NavigationMapView.Direction"
            );

        directionPicker.Select("Top to bottom");
        await Wait(1, TestContext.Current.CancellationToken);
        map.ItemStatus
            .Should()
            .Be(
                "TopToBottom",
                "because the direction binding should also support switching back at runtime"
            );
    }

    [Fact]
    public async Task Settings_ShouldBeAvailable_ThroughAutoSuggestBox()
    {
        AutomationElement? autoSuggestBox = FindFirst("NavigationAutoSuggestBox");

        autoSuggestBox
            .Should()
            .NotBeNull("because NavigationAutoSuggestBox should be present in the main window");

        autoSuggestBox.Click();

        Enter("Settings");

        await Wait(1, TestContext.Current.CancellationToken);

        FindFirst(c => c.ByText("About"))
            .Should()
            .NotBeNull("because Settings page should be displayed after clicking the Settings button");
    }

    [Fact]
    public async Task Settings_ShouldBeAvailable_ThroughNavigation()
    {
        AutomationElement? settingsButton = FindFirst("NavigationFooterItems")
            ?.FindFirstDescendant(c => c.ByText("Settings"));

        settingsButton.Should().NotBeNull("because NavigationView should be present in the main window");
        settingsButton.Click();

        await Wait(1, TestContext.Current.CancellationToken);

        FindFirst(c => c.ByText("About"))
            .Should()
            .NotBeNull("because Settings page should be displayed after clicking the Settings button");
    }
}
