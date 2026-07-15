// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines the template parts for the <see cref="NavigationView"/> control
/// </content>
[TemplatePart(Name = PartContentPresenterHost, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PartMenuItemsList,        Type = typeof(NavigationViewList))]
[TemplatePart(Name = PartFooterMenuItemsList,  Type = typeof(NavigationViewList))]
[TemplatePart(Name = PartPaneButtonsPresenter, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PartAutoSuggestBoxButton, Type = typeof(System.Windows.Controls.Button))]
public partial class NavigationView
{
    /// <summary>
    /// Template slot that hosts the control-owned <see cref="NavigationViewContentPresenter"/>.
    /// Keeping the navigation frame outside the generated template subtree preserves the current
    /// page and its journal when <see cref="PaneDisplayMode"/> selects another template.
    /// </summary>
    private const string PartContentPresenterHost =
        "PART_NavigationViewContentPresenterHost";

    /// <summary>
    /// Template element represented by the <c>PART_MenuItemsList</c> name.
    /// </summary>
    private const string PartMenuItemsList = "PART_MenuItemsList";

    /// <summary>
    /// Template element represented by the <c>PART_FooterMenuItemsList</c> name.
    /// </summary>
    private const string PartFooterMenuItemsList = "PART_FooterMenuItemsList";

    /// <summary>
    /// Template element that receives navigation-owned pane buttons which are not
    /// currently contributed to a title bar.
    /// </summary>
    private const string PartPaneButtonsPresenter = "PART_PaneButtonsPresenter";

    /// <summary>
    /// Template element represented by the <c>PART_AutoSuggestBoxSymbolButton</c> name.
    /// </summary>
    private const string PartAutoSuggestBoxButton = "PART_AutoSuggestBoxSymbolButton";

    /// <summary>
    /// Gets the stable frame responsible for navigation and rendering page content.
    /// Unlike visual template parts, this instance has the same lifetime as the
    /// <see cref="NavigationView"/> so changing display modes cannot discard navigation state.
    /// </summary>
    private NavigationViewContentPresenter NavigationViewContentPresenter { get; } = new();

    /// <summary>
    /// Gets or sets the template slot that displays <see cref="NavigationViewContentPresenter"/>.
    /// Every built-in template provides this required part; it is cleared before a new template is
    /// attached because a WPF element cannot simultaneously have two visual parents.
    /// </summary>
    private ContentPresenter NavigationViewContentPresenterHost { get; set; } = null!;

    /// <summary>
    /// Gets or sets the required list that hosts the primary navigation menu.
    /// </summary>
    private NavigationViewList MenuItemsList { get; set; } = null!;

    /// <summary>
    /// Gets or sets the required list that hosts the footer navigation menu.
    /// </summary>
    private NavigationViewList FooterMenuItemsList { get; set; } = null!;

    /// <summary>
    /// Gets or sets the template slot used for navigation-owned buttons that remain in the pane.
    /// The presenter is optional so custom templates can deliberately omit the pane command area.
    /// </summary>
    private ContentPresenter? PaneButtonsPresenter { get; set; }

    /// <summary>
    /// Gets or sets the control that is visitable if PaneDisplayMode="Left" and in compact state
    /// </summary>
    private ButtonBase? AutoSuggestBoxSymbolButton { get; set; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // Template invalidation removes the old generated subtree before WPF calls this method,
        // but a ContentPresenter can still retain its Content reference. Release both control-owned
        // children explicitly before assigning them to slots in the newly generated subtree.
        NavigationViewContentPresenterHost?.SetCurrentValue(
            ContentPresenter.ContentProperty,
            null
        );

        PaneButtonsPresenter?.SetCurrentValue(
            ContentPresenter.ContentProperty,
            null
        );

        AutoSuggestBoxSymbolButton?.Click -= AutoSuggestBoxSymbolButtonOnClick;
        AutoSuggestBoxSymbolButton = null;

        base.OnApplyTemplate();

        NavigationViewContentPresenterHost = RequireTemplateChild<ContentPresenter>(
            PartContentPresenterHost
        );

        MenuItemsList = RequireTemplateChild<NavigationViewList>(
            PartMenuItemsList
        );

        FooterMenuItemsList = RequireTemplateChild<NavigationViewList>(
            PartFooterMenuItemsList
        );

        PaneButtonsPresenter = GetTemplateChild(PartPaneButtonsPresenter) as ContentPresenter;

        MenuItemsList.SetCurrentValue(
            ItemsControl.ItemsSourceProperty,
            MenuItems
        );

        FooterMenuItemsList.SetCurrentValue(
            ItemsControl.ItemsSourceProperty,
            FooterMenuItems
        );

        NavigationViewContentPresenterHost.SetCurrentValue(
            ContentPresenter.ContentProperty,
            NavigationViewContentPresenter
        );

        if (
            GetTemplateChild(PartAutoSuggestBoxButton)
            is ButtonBase autoSuggestBoxSymbolButton
        )
        {
            AutoSuggestBoxSymbolButton = autoSuggestBoxSymbolButton;

            AutoSuggestBoxSymbolButton.Click -= AutoSuggestBoxSymbolButtonOnClick;
            AutoSuggestBoxSymbolButton.Click += AutoSuggestBoxSymbolButtonOnClick;
        }

        // Buttons are owned by NavigationView rather than the template because placement is
        // chosen at runtime and can target TitleBar.LeftContent. Populate the newly applied
        // slot only after the old template presenter has released its visual child.
        UpdatePaneButtonPlacement();

        // Fluent and the horizontal templates do not define PaneStates, so resetting the old
        // template cannot clear a compact state retained by VisualStateManager. Reset the newly
        // attached compact state group before entering its target state to guarantee that its
        // storyboard starts after a cross-template mode change.
        _ = VisualStateManager.GoToState(this, "PaneReset", false);
        UpdateVisualState(this);
    }

    private T RequireTemplateChild<T>(string name)
        where T : DependencyObject
    {
        return GetTemplateChild(name) as T ?? throw new ArgumentNullException(name);
    }
}
