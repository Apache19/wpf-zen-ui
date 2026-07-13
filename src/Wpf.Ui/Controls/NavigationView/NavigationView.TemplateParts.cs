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
[TemplatePart(Name = TemplateElementNavigationViewContentPresenter, Type = typeof(NavigationViewContentPresenter))]
[TemplatePart(Name = TemplateElementMenuItemsItemsControl,          Type = typeof(System.Windows.Controls.ItemsControl))]
[TemplatePart(Name = TemplateElementFooterMenuItemsItemsControl,    Type = typeof(System.Windows.Controls.ItemsControl))]
[TemplatePart(Name = TemplateElementPaneButtonsPresenter,           Type = typeof(System.Windows.Controls.ContentPresenter))]
[TemplatePart(Name = TemplateElementAutoSuggestBoxSymbolButton,     Type = typeof(System.Windows.Controls.Button))]
public partial class NavigationView
{
    /// <summary>
    /// Template element represented by the <c>PART_MenuItemsItemsControl</c> name.
    /// </summary>
    private const string TemplateElementNavigationViewContentPresenter =
        "PART_NavigationViewContentPresenter";

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
    /// Gets or sets the control responsible for rendering the content.
    /// </summary>
    protected NavigationViewContentPresenter NavigationViewContentPresenter { get; set; } = null!;

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
        if (PaneButtonsPresenter is not null)
        {
            PaneButtonsPresenter.SetCurrentValue(System.Windows.Controls.ContentPresenter.ContentProperty, null);
        }

        base.OnApplyTemplate();

        NavigationViewContentPresenter = GetTemplateChild<NavigationViewContentPresenter>(
            TemplateElementNavigationViewContentPresenter
        );
        MenuItemsItemsControl = GetTemplateChild<System.Windows.Controls.ItemsControl>(
            TemplateElementMenuItemsItemsControl
        );
        FooterMenuItemsItemsControl = GetTemplateChild<System.Windows.Controls.ItemsControl>(
            TemplateElementFooterMenuItemsItemsControl
        );
        PaneButtonsPresenter = GetTemplateChild(TemplateElementPaneButtonsPresenter)
            as System.Windows.Controls.ContentPresenter;

        MenuItemsItemsControl.SetCurrentValue(
            System.Windows.Controls.ItemsControl.ItemsSourceProperty,
            MenuItems
        );
        FooterMenuItemsItemsControl.SetCurrentValue(
            System.Windows.Controls.ItemsControl.ItemsSourceProperty,
            FooterMenuItems
        );

        if (NavigationViewContentPresenter is not null)
        {
            NavigationViewContentPresenter.Navigated -= OnNavigationViewContentPresenterNavigated;
            NavigationViewContentPresenter.Navigated += OnNavigationViewContentPresenterNavigated;
        }

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
