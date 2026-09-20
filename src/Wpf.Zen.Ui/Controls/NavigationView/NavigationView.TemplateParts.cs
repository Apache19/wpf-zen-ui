// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines the template parts for the <see cref="NavigationView"/> control
/// </content>
[TemplatePart(Name = PartNavigationFrameHost, Type = typeof(ContentControl))]
[TemplatePart(Name = PartMenuItemsList, Type = typeof(NavigationViewList))]
[TemplatePart(Name = PartFooterMenuItemsList, Type = typeof(NavigationViewList))]
[TemplatePart(Name = PartPaneButtonsPresenter, Type = typeof(ContentPresenter))]
[TemplatePart(Name = PartAutoSuggestBoxButton, Type = typeof(System.Windows.Controls.Button))]
public partial class NavigationView
{
    /// <summary>
    /// Template host that presents the control-owned WPF <see cref="Frame"/> either directly or
    /// inside a dynamic scroll viewer. Keeping the frame outside the generated template subtree
    /// preserves the current page and navigation service when <see cref="PaneDisplayMode"/> selects
    /// another template.
    /// </summary>
    private const string PartNavigationFrameHost = "PART_NavigationFrameHost";

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
    /// The stable WPF navigation surface owned by this control.
    /// </summary>
    /// <remarks>
    /// This is intentionally an implementation field rather than a public presenter control. A
    /// native <see cref="Frame"/> already supplies the required content and navigation behavior;
    /// the surrounding template independently supplies optional host scrolling.
    /// </remarks>
    private readonly Frame _navigationFrame = CreateNavigationFrame();

    /// <summary>
    /// Gets or sets the template slot that displays the control-owned navigation frame. Every
    /// built-in template provides this required part; it is cleared before a new template is
    /// attached because a WPF element cannot simultaneously have two visual parents. The slot is
    /// null until the first template is applied.
    /// </summary>
    protected ContentControl? NavigationFrameHost { get; set; }

    /// <summary>
    /// Gets or sets the required list that hosts the primary navigation menu.
    /// Null until the first template is applied.
    /// </summary>
    protected NavigationViewList? MenuItemsList { get; set; }

    /// <summary>
    /// Gets or sets the required list that hosts the footer navigation menu.
    /// Null until the first template is applied.
    /// </summary>
    protected NavigationViewList? FooterMenuItemsList { get; set; }

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
    protected ContentPresenter? PaneButtonsPresenter { get; set; }

    /// <summary>
    /// Gets or sets the control that is visitable if PaneDisplayMode="Left" and in compact state
    /// </summary>
    protected ButtonBase? AutoSuggestBoxSymbolButton { get; set; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // PaneGrid binds to the retained width, so initialize that value before WPF creates the
        // first Left subtree. A later Left application deliberately keeps the captured value.
        EnsureLeftPaneWidthInitialized();

        // Template invalidation removes the old generated subtree before WPF calls this method,
        // but a ContentPresenter can still retain its Content reference. Release both control-owned
        // children explicitly before assigning them to slots in the newly generated subtree.
        if (NavigationFrameHost is not null)
        {
            NavigationFrameHost.SetCurrentValue(ContentControl.ContentProperty, null);
        }

        if (PaneButtonsPresenter is not null)
        {
            PaneButtonsPresenter.SetCurrentValue(ContentPresenter.ContentProperty, null);
        }

        if (AutoSuggestBoxSymbolButton is not null)
        {
            AutoSuggestBoxSymbolButton.Click -= AutoSuggestBoxSymbolButtonOnClick;
            AutoSuggestBoxSymbolButton = null;
        }

        base.OnApplyTemplate();

        NavigationFrameHost = RequireTemplateChild<ContentControl>(PartNavigationFrameHost);
        MenuItemsList = RequireTemplateChild<NavigationViewList>(PartMenuItemsList);
        FooterMenuItemsList = RequireTemplateChild<NavigationViewList>(PartFooterMenuItemsList);
        PaneButtonsPresenter = GetTemplateChild(PartPaneButtonsPresenter) as ContentPresenter;

        UpdateMenuItemsHosts();

        NavigationFrameHost.SetCurrentValue(ContentControl.ContentProperty, _navigationFrame);

        if (GetTemplateChild(PartAutoSuggestBoxButton) is ButtonBase autoSuggestBoxSymbolButton)
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

    private T RequireTemplateChild<T>(string name)
        where T : DependencyObject
    {
        return GetTemplateChild(name) as T ?? throw new ArgumentNullException(name);
    }
}
