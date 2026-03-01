// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ListView"/>, and adds customized support <see cref="ListViewViewState.GridView"/> or <see cref="ListViewViewState.Default"/>.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:ListView ItemsSource="{Binding ...}" &gt;
///     &lt;ui:ListView.View&gt;
///         &lt;ui:GridView&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding FirstName}"
///                 Header="First Name" /&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding LastName}"
///                 Header="Last Name" /&gt;
///         &lt;/ui:GridView&gt;
///     &lt;/ui:ListView.View&gt;
/// &lt;/ui:ListView&gt;
/// </code>
/// </example>
public class ListView : System.Windows.Controls.ListView
{
    // todo 移除 ViewStateProperty，只提供GridView的默认样式，并把ListView的默认样式应用上去即可，不要做过多的改变。

    /// <summary>Identifies the <see cref="ViewState"/> dependency property.</summary>
    public static readonly DependencyProperty ViewStateProperty = DependencyProperty.Register(
        nameof(ViewState),
        typeof(ListViewViewState),
        typeof(ListView),
        new FrameworkPropertyMetadata(ListViewViewState.Default, OnViewStateChanged)
    );

    static ListView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ListView),
            new FrameworkPropertyMetadata(typeof(ListView))
        );

        ViewProperty.OverrideMetadata(
            typeof(ListView),
            new PropertyMetadata(
                static (d, e) =>
                {
                    ((ListView)d).UpdateViewState();
                }
            )
        );
    }

    /// <summary>
    /// Gets or sets the view state of the <see cref="ListView"/>, enabling custom logic based on the current view.
    /// </summary>
    /// <value>The current view state of the <see cref="ListView"/>.</value>
    public ListViewViewState ViewState
    {
        get => (ListViewViewState)GetValue(ViewStateProperty);
        set => SetValue(ViewStateProperty, value);
    }

    private static void OnViewStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView self)
        {
            return;
        }

        self.OnViewStateChanged(e);
    }

    protected virtual void OnViewStateChanged(DependencyPropertyChangedEventArgs e)
    {
        // Hook for derived classes to react to ViewState property changes
    }

    public ListView()
    {
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateViewState(); // set the initial state
    }

    private void UpdateViewState()
    {
        ListViewViewState viewState = View switch
        {
            System.Windows.Controls.GridView => ListViewViewState.GridView,
            null                             => ListViewViewState.Default,
            _                                => ListViewViewState.Default,
        };

        SetCurrentValue(ViewStateProperty, viewState);
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new ListViewItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is ListViewItem;
    }
}