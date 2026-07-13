// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

// ReSharper disable once CheckNamespace

using System.Windows.Controls.Primitives;

namespace Wpf.Ui.Controls;

/// <content>
/// Defines the template parts for the <see cref="NavigationView"/> control
/// </content>
[TemplatePart(Name = TemplateElementNavigationViewContentPresenterHost, Type = typeof(System.Windows.Controls.ContentPresenter))]
[TemplatePart(Name = TemplateElementMenuItemsItemsControl,              Type = typeof(System.Windows.Controls.ItemsControl))]
[TemplatePart(Name = TemplateElementFooterMenuItemsItemsControl,        Type = typeof(System.Windows.Controls.ItemsControl))]
[TemplatePart(Name = TemplateElementPaneButtonsPresenter,               Type = typeof(System.Windows.Controls.ContentPresenter))]
[TemplatePart(Name = TemplateElementAutoSuggestBoxSymbolButton,         Type = typeof(System.Windows.Controls.Button))]
public partial class NavigationView
{
    /// <summary>
    /// Template slot that hosts the control-owned <see cref="NavigationViewContentPresenter"/>.
    /// Keeping the navigation frame outside the generated template subtree preserves the current
    /// page and its journal when <see cref="PaneDisplayMode"/> selects another template.
    /// </summary>
    private const string TemplateElementNavigationViewContentPresenterHost =
        "PART_NavigationViewContentPresenterHost";

    /// <summary>
    /// Template element represented by the <c>PART_MenuItemsItemsControl</c> name.
    /// </summary>
    private const string TemplateElementMenuItemsItemsControl = "PART_MenuItemsItemsControl";

    /// <summary>
    /// Template element represented by the <c>PART_FooterMenuItemsItemsControl</c> name.
    /// </summary>
    private const string TemplateElementFooterMenuItemsItemsControl = "PART_FooterMenuItemsItemsControl";

    /// <summary>
    /// Template element that receives navigation-owned pane buttons which are not
    /// currently contributed to a title bar.
    /// </summary>
    private const string TemplateElementPaneButtonsPresenter = "PART_PaneButtonsPresenter";

    /// <summary>
    /// Template element represented by the <c>PART_AutoSuggestBoxSymbolButton</c> name.
    /// </summary>
    private const string TemplateElementAutoSuggestBoxSymbolButton = "PART_AutoSuggestBoxSymbolButton";

    /// <summary>
    /// Gets the stable frame responsible for navigation and rendering page content.
    /// Unlike visual template parts, this instance has the same lifetime as the
    /// <see cref="NavigationView"/> so changing display modes cannot discard navigation state.
    /// </summary>
    protected NavigationViewContentPresenter NavigationViewContentPresenter { get; } = new();

    /// <summary>
    /// Gets or sets the template slot that displays <see cref="NavigationViewContentPresenter"/>.
    /// Every built-in template provides this required part; it is cleared before a new template is
    /// attached because a WPF element cannot simultaneously have two visual parents.
    /// </summary>
    protected System.Windows.Controls.ContentPresenter NavigationViewContentPresenterHost { get; set; } =
        null!;

    /// <summary>
    /// Gets or sets the control located at the top of the pane with left arrow icon.
    /// </summary>
    protected System.Windows.Controls.ItemsControl MenuItemsItemsControl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the control located at the top of the pane with hamburger icon.
    /// </summary>
    protected System.Windows.Controls.ItemsControl FooterMenuItemsItemsControl { get; set; } = null!;

    /// <summary>
    /// Gets or sets the control located at the top of the pane with left arrow icon.
    /// </summary>
    protected NavigationViewButton? BackButton { get; set; }

    /// <summary>
    /// Gets or sets the control located at the top of the pane with hamburger icon.
    /// </summary>
    protected NavigationViewButton? ToggleButton { get; set; }

    /// <summary>
    /// Gets or sets the template slot used for navigation-owned buttons that remain in the pane.
    /// The presenter is optional so custom templates can deliberately omit the pane command area.
    /// </summary>
    protected System.Windows.Controls.ContentPresenter? PaneButtonsPresenter { get; set; }

    /// <summary>
    /// Gets or sets the control that is visitable if PaneDisplayMode="Left" and in compact state
    /// </summary>
    protected ButtonBase? AutoSuggestBoxSymbolButton { get; set; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // Template invalidation removes the old generated subtree before WPF calls this method,
        // but a ContentPresenter can still retain its Content reference. Release both control-owned
        // children explicitly before assigning them to slots in the newly generated subtree.
        if (NavigationViewContentPresenterHost is not null)
        {
            NavigationViewContentPresenterHost.SetCurrentValue(
                System.Windows.Controls.ContentPresenter.ContentProperty,
                null
            );
        }

        if (PaneButtonsPresenter is not null)
        {
            PaneButtonsPresenter.SetCurrentValue(
                System.Windows.Controls.ContentPresenter.ContentProperty,
                null
            );
        }

        if (AutoSuggestBoxSymbolButton is not null)
        {
            AutoSuggestBoxSymbolButton.Click -= AutoSuggestBoxSymbolButtonOnClick;
            AutoSuggestBoxSymbolButton       =  null;
        }

        base.OnApplyTemplate();

        NavigationViewContentPresenterHost = GetTemplateChild<System.Windows.Controls.ContentPresenter>(
            TemplateElementNavigationViewContentPresenterHost
        );
        MenuItemsItemsControl = GetTemplateChild<System.Windows.Controls.ItemsControl>(
            TemplateElementMenuItemsItemsControl
        );
        FooterMenuItemsItemsControl = GetTemplateChild<System.Windows.Controls.ItemsControl>(
            TemplateElementFooterMenuItemsItemsControl
        );
        PaneButtonsPresenter =
            GetTemplateChild(TemplateElementPaneButtonsPresenter) as System.Windows.Controls.ContentPresenter;

        MenuItemsItemsControl.SetCurrentValue(
            System.Windows.Controls.ItemsControl.ItemsSourceProperty,
            MenuItems
        );
        FooterMenuItemsItemsControl.SetCurrentValue(
            System.Windows.Controls.ItemsControl.ItemsSourceProperty,
            FooterMenuItems
        );

        NavigationViewContentPresenterHost.SetCurrentValue(
            System.Windows.Controls.ContentPresenter.ContentProperty,
            NavigationViewContentPresenter
        );

        if (
            GetTemplateChild(TemplateElementAutoSuggestBoxSymbolButton)
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
        UpdateVisualState(this);
    }

    protected T GetTemplateChild<T>(string name)
        where T : DependencyObject
    {
        if (GetTemplateChild(name) is not T dependencyObject)
        {
            throw new ArgumentNullException(name);
        }

        return dependencyObject;
    }
}