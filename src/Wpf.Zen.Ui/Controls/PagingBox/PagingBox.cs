using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using NativeComboBox = System.Windows.Controls.ComboBox;
using NativeTextBox = System.Windows.Controls.TextBox;

// ReSharper disable MemberCanBePrivate.Global

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides paging navigation without querying data or inferring application state.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CurrentPage"/> and <see cref="PageCount"/> represent paging state supplied by the consumer.
/// User navigation invokes <see cref="Command"/> and raises <see cref="PageRequested"/>,
/// but does not optimistically change the current page. The command parameter is always the requested page number.
/// </para>
/// <para>
/// The control is a composite <see cref="ICommandSource"/> with multiple navigation targets. Each button and page
/// item calls <see cref="ICommand.CanExecute(object)"/> with its own target page, so the command disables only
/// disallowed actions instead of incorrectly disabling the entire control for the current page.
/// </para>
/// <para>
/// The page-size selector is optional: it is hidden when <see cref="PageSizeOptions"/> is <see langword="null"/>
/// and shown when options are supplied. External updates to <see cref="PageSize"/> only synchronize the display.
/// Selecting, or when permitted entering, a valid new page size raises <see cref="PageSizeChanged"/> and invokes
/// <see cref="Command"/> for page 1.
/// </para>
/// </remarks>
[TemplatePart(Name = PreviousButtonPartName,     Type = typeof(ButtonBase))]
[TemplatePart(Name = NextButtonPartName,         Type = typeof(ButtonBase))]
[TemplatePart(Name = CurrentPageTextBoxPartName, Type = typeof(NativeTextBox))]
[TemplatePart(Name = PageItemsControlPartName,   Type = typeof(ItemsControl))]
[TemplatePart(Name = PageSizeComboBoxPartName,   Type = typeof(NativeComboBox))]
public class PagingBox : Control, ICommandSource
{
    private const string PreviousButtonPartName     = "PART_PreviousButton";
    private const string NextButtonPartName         = "PART_NextButton";
    private const string CurrentPageTextBoxPartName = "PART_CurrentPageTextBox";
    private const string PageItemsControlPartName   = "PART_PageItemsControl";
    private const string PageSizeComboBoxPartName   = "PART_PageSizeComboBox";

    private ButtonBase?     _previousButton;
    private ButtonBase?     _nextButton;
    private NativeTextBox?  _currentPageTextBox;
    private ItemsControl?   _pageItemsControl;
    private NativeComboBox? _pageSizeComboBox;
    private bool            _isSynchronizingPageSize;
    private int             _userActionDepth;

    /// <summary>Gets the minimum value supported by <see cref="MaxVisiblePages"/>.</summary>
    public const int MinimumVisiblePages = 5;

    /// <summary>
    /// Gets the maximum value supported by <see cref="MaxVisiblePages"/>.
    /// </summary>
    /// <remarks>
    /// Page-list entries are directly rendered interactive elements. Bounding the list prevents a bad configuration
    /// from allocating a large number of elements at once; larger data sets should use ellipsis navigation.
    /// </remarks>
    public const int MaximumVisiblePages = 99;

    /// <summary>
    /// Gets standard page-size options that can be assigned directly to <see cref="PageSizeOptions"/>.
    /// </summary>
    public static IReadOnlyList<int> StandardPageSizeOptions { get; } = new ReadOnlyCollection<int>([10, 15, 20, 30, 50]);

    /// <summary>Gets the default maximum allowed page size.</summary>
    public const int DefaultMaximumPageSize = 1000;

    #region DP: Command

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty =
        ButtonBase.CommandProperty.AddOwner(
            typeof(PagingBox),
            new FrameworkPropertyMetadata(null, OnCommandChanged)
        );

    /// <summary>
    /// Gets or sets the command executed when the user explicitly requests paging.
    /// </summary>
    /// <remarks>
    /// The command parameter is the target page number as an <see cref="int"/>. When the user selects or enters a
    /// new page size, the control commits <see cref="PageSize"/> before executing the command with page 1.
    /// </remarks>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    #endregion

    #region Read-Only DP: CommandParameter

    private static readonly DependencyPropertyKey CommandParameterPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CommandParameter),
            typeof(object),
            typeof(PagingBox),
            new PropertyMetadata(1)
        );

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty =
        CommandParameterPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the target page of the current user action, or the consumer-supplied current page when no action is in progress.
    /// </summary>
    /// <remarks>The control manages this value; command execution receives the same target-page snapshot.</remarks>
    public object CommandParameter => GetValue(CommandParameterProperty);

    #endregion

    #region DP: CommandTarget

    /// <summary>
    /// Identifies the <see cref="CommandTarget"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CommandTargetProperty =
        ButtonBase.CommandTargetProperty.AddOwner(
            typeof(PagingBox),
            new FrameworkPropertyMetadata(null, OnCommandTargetChanged)
        );

    /// <summary>Gets or sets the target element for a routed command.</summary>
    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    #endregion

    #region DP: DisplayMode

    /// <summary>Identifies the <see cref="DisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayModeProperty =
        DependencyProperty.Register(
            nameof(DisplayMode),
            typeof(PagingBoxDisplayMode),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                PagingBoxDisplayMode.Complete,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            validateValueCallback: static
                value => value is PagingBoxDisplayMode mode
                         && Enum.IsDefined(typeof(PagingBoxDisplayMode), mode)
        );

    /// <summary>Gets or sets the paging-navigation layout.</summary>
    public PagingBoxDisplayMode DisplayMode
    {
        get => (PagingBoxDisplayMode)GetValue(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    #endregion

    #region DP: PageSizeLabel

    /// <summary>Identifies the <see cref="PageSizeLabel"/> dependency property.</summary>
    public static readonly DependencyProperty PageSizeLabelProperty =
        RegisterTextProperty(
            nameof(PageSizeLabel),
            PagingBoxResources.PageSizeLabel
        );

    /// <summary>Gets or sets the text displayed before the page-size selector.</summary>
    public string PageSizeLabel
    {
        get => (string)GetValue(PageSizeLabelProperty);
        set => SetValue(PageSizeLabelProperty, value);
    }

    #endregion

    #region DP: PreviousPageText

    /// <summary>Identifies the <see cref="PreviousPageText"/> dependency property.</summary>
    public static readonly DependencyProperty PreviousPageTextProperty =
        RegisterTextProperty(
            nameof(PreviousPageText),
            PagingBoxResources.PreviousPage
        );

    /// <summary>Gets or sets the previous-page button text and accessible name.</summary>
    public string PreviousPageText
    {
        get => (string)GetValue(PreviousPageTextProperty);
        set => SetValue(PreviousPageTextProperty, value);
    }

    #endregion

    #region DP: NextPageText

    /// <summary>Identifies the <see cref="NextPageText"/> dependency property.</summary>
    public static readonly DependencyProperty NextPageTextProperty =
        RegisterTextProperty(
            nameof(NextPageText),
            PagingBoxResources.NextPage
        );

    /// <summary>Gets or sets the next-page button text and accessible name.</summary>
    public string NextPageText
    {
        get => (string)GetValue(NextPageTextProperty);
        set => SetValue(NextPageTextProperty, value);
    }

    #endregion

    #region DP: PageSizeSelectorAutomationName

    /// <summary>
    /// Identifies the <see cref="PageSizeSelectorAutomationName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageSizeSelectorAutomationNameProperty =
        RegisterTextProperty(
            nameof(PageSizeSelectorAutomationName),
            PagingBoxResources.PageSizeSelectorAutomationName
        );

    /// <summary>Gets or sets the accessible name of the page-size selector.</summary>
    public string PageSizeSelectorAutomationName
    {
        get => (string)GetValue(PageSizeSelectorAutomationNameProperty);
        set => SetValue(PageSizeSelectorAutomationNameProperty, value);
    }

    #endregion

    #region DP: CurrentPageInputAutomationName

    /// <summary>
    /// Identifies the <see cref="CurrentPageInputAutomationName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentPageInputAutomationNameProperty =
        RegisterTextProperty(
            nameof(CurrentPageInputAutomationName),
            PagingBoxResources.CurrentPageInputAutomationName
        );

    /// <summary>Gets or sets the accessible name of the current-page input.</summary>
    public string CurrentPageInputAutomationName
    {
        get => (string)GetValue(CurrentPageInputAutomationNameProperty);
        set => SetValue(CurrentPageInputAutomationNameProperty, value);
    }

    #endregion

    #region DP: CurrentPageInputToolTip

    /// <summary>
    /// Identifies the <see cref="CurrentPageInputToolTip"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentPageInputToolTipProperty =
        RegisterTextProperty(
            nameof(CurrentPageInputToolTip),
            PagingBoxResources.CurrentPageInputToolTip
        );

    /// <summary>Gets or sets the tooltip for the current-page input.</summary>
    public string CurrentPageInputToolTip
    {
        get => (string)GetValue(CurrentPageInputToolTipProperty);
        set => SetValue(CurrentPageInputToolTipProperty, value);
    }

    #endregion

    #region DP: PageItemAutomationNameFormat

    /// <summary>
    /// Identifies the <see cref="PageItemAutomationNameFormat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageItemAutomationNameFormatProperty =
        RegisterPageFormatProperty(
            nameof(PageItemAutomationNameFormat),
            PagingBoxResources.PageItemAutomationNameFormat
        );

    /// <summary>
    /// Gets or sets the accessible-name format for a regular page item, where {0} is the page number.
    /// </summary>
    public string PageItemAutomationNameFormat
    {
        get => (string)GetValue(PageItemAutomationNameFormatProperty);
        set => SetValue(PageItemAutomationNameFormatProperty, value);
    }

    #endregion

    #region DP: CurrentPageItemAutomationNameFormat

    /// <summary>
    /// Identifies the <see cref="CurrentPageItemAutomationNameFormat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentPageItemAutomationNameFormatProperty =
        RegisterPageFormatProperty(
            nameof(CurrentPageItemAutomationNameFormat),
            PagingBoxResources.CurrentPageItemAutomationNameFormat
        );

    /// <summary>
    /// Gets or sets the accessible-name format for the current-page item, where {0} is the page number.
    /// </summary>
    public string CurrentPageItemAutomationNameFormat
    {
        get => (string)GetValue(CurrentPageItemAutomationNameFormatProperty);
        set => SetValue(CurrentPageItemAutomationNameFormatProperty, value);
    }

    #endregion

    #region DP: PreviousEllipsisAutomationNameFormat

    /// <summary>
    /// Identifies the <see cref="PreviousEllipsisAutomationNameFormat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PreviousEllipsisAutomationNameFormatProperty =
        RegisterPageFormatProperty(
            nameof(PreviousEllipsisAutomationNameFormat),
            PagingBoxResources.PreviousEllipsisAutomationNameFormat
        );

    /// <summary>
    /// Gets or sets the accessible-name format for the backward ellipsis item, where {0} is the target page.
    /// </summary>
    public string PreviousEllipsisAutomationNameFormat
    {
        get => (string)GetValue(PreviousEllipsisAutomationNameFormatProperty);
        set => SetValue(PreviousEllipsisAutomationNameFormatProperty, value);
    }

    #endregion

    #region DP: NextEllipsisAutomationNameFormat

    /// <summary>
    /// Identifies the <see cref="NextEllipsisAutomationNameFormat"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NextEllipsisAutomationNameFormatProperty =
        RegisterPageFormatProperty(
            nameof(NextEllipsisAutomationNameFormat),
            PagingBoxResources.NextEllipsisAutomationNameFormat
        );

    /// <summary>
    /// Gets or sets the accessible-name format for the forward ellipsis item, where {0} is the target page.
    /// </summary>
    public string NextEllipsisAutomationNameFormat
    {
        get => (string)GetValue(NextEllipsisAutomationNameFormatProperty);
        set => SetValue(NextEllipsisAutomationNameFormatProperty, value);
    }

    #endregion

    #region DP: MaxVisiblePages

    /// <summary>
    /// Identifies the <see cref="MaxVisiblePages"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxVisiblePagesProperty =
        DependencyProperty.Register(
            nameof(MaxVisiblePages),
            typeof(int),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                9,
                FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnPageListParameterChanged,
                CoerceMaxVisiblePages
            )
        );

    /// <summary>
    /// Gets or sets the maximum number of items displayed in the page list. Valid values range from 5 through 99;
    /// even values are coerced down to the nearest odd value.
    /// </summary>
    public int MaxVisiblePages
    {
        get => (int)GetValue(MaxVisiblePagesProperty);
        set => SetValue(MaxVisiblePagesProperty, value);
    }

    #endregion

    #region DP: EllipsisStep

    /// <summary>Identifies the <see cref="EllipsisStep"/> dependency property.</summary>
    public static readonly DependencyProperty EllipsisStepProperty =
        DependencyProperty.Register(
            nameof(EllipsisStep),
            typeof(int),
            typeof(PagingBox),
            new PropertyMetadata(0, OnPageListParameterChanged, CoerceEllipsisStep)
        );

    /// <summary>
    /// Gets or sets the navigation step of ellipsis items. A value of 0 derives the step from the visible page range.
    /// </summary>
    public int EllipsisStep
    {
        get => (int)GetValue(EllipsisStepProperty);
        set => SetValue(EllipsisStepProperty, value);
    }

    #endregion

    #region DP: CurrentPage

    /// <summary>Identifies the <see cref="CurrentPage"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(
            nameof(CurrentPage),
            typeof(int),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPagingStateChanged,
                CoerceCurrentPage
            )
        );

    /// <summary>
    /// Gets or sets the consumer-supplied current page number, starting at 1.
    /// </summary>
    public int CurrentPage
    {
        get => (int)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    #endregion

    #region DP: PageCount

    /// <summary>Identifies the <see cref="PageCount"/> dependency property.</summary>
    public static readonly DependencyProperty PageCountProperty =
        DependencyProperty.Register(
            nameof(PageCount),
            typeof(int),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnPagingStateChanged,
                CoercePageCount
            )
        );

    /// <summary>
    /// Gets or sets the total page count calculated by the consumer. A value of 0 indicates that no pageable data is available.
    /// </summary>
    public int PageCount
    {
        get => (int)GetValue(PageCountProperty);
        set => SetValue(PageCountProperty, value);
    }

    #endregion

    #region DP: PageSize

    /// <summary>Identifies the <see cref="PageSize"/> dependency property.</summary>
    public static readonly DependencyProperty PageSizeProperty = DependencyProperty.Register(
        nameof(PageSize),
        typeof(int),
        typeof(PagingBox),
        new FrameworkPropertyMetadata(
            20,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPageSizeChanged,
            CoercePageSize
        )
    );

    /// <summary>
    /// Gets or sets the current page size. Setting this property externally does not raise <see cref="PageSizeChanged"/>.
    /// </summary>
    public int PageSize
    {
        get => (int)GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    #endregion

    #region DP: MaximumPageSize

    /// <summary>Identifies the <see cref="MaximumPageSize"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumPageSizeProperty =
        DependencyProperty.Register(
            nameof(MaximumPageSize),
            typeof(int),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                DefaultMaximumPageSize,
                OnMaximumPageSizeChanged
            ),
            validateValueCallback: static value => value is >= 1
        );

    /// <summary>
    /// Gets or sets the maximum allowed page size. This limit applies to external assignments, list selections, and manual input.
    /// </summary>
    /// <remarks>
    /// This client-side limit protects the user experience; servers must still validate paging parameters independently.
    /// </remarks>
    public int MaximumPageSize
    {
        get => (int)GetValue(MaximumPageSizeProperty);
        set => SetValue(MaximumPageSizeProperty, value);
    }

    #endregion

    #region DP: IsPageSizeEditable

    /// <summary>Identifies the <see cref="IsPageSizeEditable"/> dependency property.</summary>
    public static readonly DependencyProperty IsPageSizeEditableProperty =
        DependencyProperty.Register(
            nameof(IsPageSizeEditable),
            typeof(bool),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Gets or sets a value indicating whether the user can enter a page size that is not present in <see cref="PageSizeOptions"/>.
    /// </summary>
    public bool IsPageSizeEditable
    {
        get => (bool)GetValue(IsPageSizeEditableProperty);
        set => SetValue(IsPageSizeEditableProperty, value);
    }

    #endregion

    #region DP: PageSizeOptions

    /// <summary>Identifies the <see cref="PageSizeOptions"/> dependency property.</summary>
    public static readonly DependencyProperty PageSizeOptionsProperty =
        DependencyProperty.Register(
            nameof(PageSizeOptions),
            typeof(IEnumerable),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure,
                OnPageSizeOptionsChanged
            )
        );

    /// <summary>
    /// Gets or sets the page-size options. The page-size selector is hidden when this value is <see langword="null"/>.
    /// </summary>
    public IEnumerable? PageSizeOptions
    {
        get => (IEnumerable?)GetValue(PageSizeOptionsProperty);
        set => SetValue(PageSizeOptionsProperty, value);
    }

    #endregion

    #region Read-Only DP: HasPages

    private static readonly DependencyPropertyKey HasPagesPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasPages),
            typeof(bool),
            typeof(PagingBox),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="HasPages"/> dependency property.</summary>
    public static readonly DependencyProperty HasPagesProperty =
        HasPagesPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether navigable pages are currently available.
    /// </summary>
    public bool HasPages => (bool)GetValue(HasPagesProperty);

    #endregion

    #region Read-Only DP: CanGoPrevious

    private static readonly DependencyPropertyKey CanGoPreviousPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanGoPrevious),
            typeof(bool),
            typeof(PagingBox),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="CanGoPrevious"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanGoPreviousProperty =
        CanGoPreviousPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the user can currently request the previous page.
    /// </summary>
    public bool CanGoPrevious => (bool)GetValue(CanGoPreviousProperty);

    #endregion

    #region Read-Only DP: CanGoNext

    private static readonly DependencyPropertyKey CanGoNextPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanGoNext),
            typeof(bool),
            typeof(PagingBox),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="CanGoNext"/> dependency property.</summary>
    public static readonly DependencyProperty CanGoNextProperty =
        CanGoNextPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the user can currently request the next page.
    /// </summary>
    public bool CanGoNext => (bool)GetValue(CanGoNextProperty);

    #endregion

    #region Read-Only DP: CanChangePageSize

    private static readonly DependencyPropertyKey CanChangePageSizePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanChangePageSize),
            typeof(bool),
            typeof(PagingBox),
            new PropertyMetadata(true)
        );

    /// <summary>
    /// Identifies the <see cref="CanChangePageSize"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanChangePageSizeProperty =
        CanChangePageSizePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the user can currently select or enter a new page size.
    /// </summary>
    public bool CanChangePageSize => (bool)GetValue(CanChangePageSizeProperty);

    #endregion

    #region Read-Only DP: PageList

    private static readonly DependencyPropertyKey PageListPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PageList),
            typeof(IReadOnlyList<PagingItem>),
            typeof(PagingBox),
            new PropertyMetadata(Array.Empty<PagingItem>())
        );

    /// <summary>
    /// Identifies the <see cref="PageList"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageListProperty =
        PageListPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the read-only page items generated by the control for its current state.
    /// </summary>
    public IReadOnlyList<PagingItem> PageList
    {
        get => (IReadOnlyList<PagingItem>)GetValue(PageListProperty);
    }

    #endregion

    #region Routed-Event: PageRequested

    /// <summary>
    /// Identifies the <see cref="PageRequested"/> routed event.
    /// </summary>
    public static readonly RoutedEvent PageRequestedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PageRequested),
            RoutingStrategy.Bubble,
            typeof(PageRequestedEventHandler),
            typeof(PagingBox)
        );

    /// <summary>
    /// Occurs when the user explicitly requests a page change through a button, page item, or input field.
    /// </summary>
    public event PageRequestedEventHandler PageRequested
    {
        add => AddHandler(PageRequestedEvent, value);
        remove => RemoveHandler(PageRequestedEvent, value);
    }

    #endregion

    #region Routed-Event: PageSizeChanged

    /// <summary>
    /// Identifies the <see cref="PageSizeChanged"/> routed event.
    /// </summary>
    public static readonly RoutedEvent PageSizeChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PageSizeChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<int>),
            typeof(PagingBox)
        );

    /// <summary>
    /// Occurs when the user explicitly selects or enters a new value in the page-size selector.
    /// External binding updates do not raise this event.
    /// </summary>
    public event RoutedPropertyChangedEventHandler<int> PageSizeChanged
    {
        add => AddHandler(PageSizeChangedEvent, value);
        remove => RemoveHandler(PageSizeChangedEvent, value);
    }

    #endregion

    static PagingBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PagingBox),
            new FrameworkPropertyMetadata(typeof(PagingBox))
        );
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new PagingBoxAutomationPeer(this);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _previousButton = GetTemplateChild(PreviousButtonPartName) as ButtonBase;
        _nextButton = GetTemplateChild(NextButtonPartName) as ButtonBase;
        _currentPageTextBox = GetTemplateChild(CurrentPageTextBoxPartName) as NativeTextBox;
        _pageItemsControl = GetTemplateChild(PageItemsControlPartName) as ItemsControl;
        _pageSizeComboBox = GetTemplateChild(PageSizeComboBoxPartName) as NativeComboBox;

        AttachTemplatePartHandlers();
        SynchronizeCurrentPageText();
        SynchronizePageSizeSelector();
        UpdatePagingState();
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // A template replacement does not guarantee that another layout pass will occur immediately, so event
        // subscriptions on the old visual tree must be released here.
        DetachTemplatePartHandlers();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>Raises the <see cref="PageRequested"/> routed event.</summary>
    /// <param name="e">The event data containing the target page number.</param>
    protected virtual void OnPageRequested(PageRequestedEventArgs e)
    {
        RaiseEvent(e);
    }

    /// <summary>Raises the <see cref="PageSizeChanged"/> routed event produced by a user action.</summary>
    /// <param name="e">The event data containing the old and new page sizes.</param>
    protected virtual void OnPageSizeChanged(RoutedPropertyChangedEventArgs<int> e)
    {
        RaiseEvent(e);
    }

    private static DependencyProperty RegisterTextProperty(string name, string defaultValue)
    {
        return DependencyProperty.Register(
            name,
            typeof(string),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                defaultValue,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            ),
            value => value is string
        );
    }

    private static DependencyProperty RegisterPageFormatProperty(string name, string defaultValue)
    {
        return DependencyProperty.Register(
            name,
            typeof(string),
            typeof(PagingBox),
            new FrameworkPropertyMetadata(
                defaultValue,
                FrameworkPropertyMetadataOptions.None,
                OnPageListParameterChanged
            ),
            IsValidPageFormat
        );
    }

    private static bool IsValidPageFormat(object value)
    {
        if (value is not string format)
        {
            return false;
        }

        try
        {
            _ = string.Format(CultureInfo.CurrentUICulture, format, 1);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static object CoerceMaxVisiblePages(DependencyObject d, object value)
    {
        var count = Clamp((int)value, MinimumVisiblePages, MaximumVisiblePages);
        return count % 2 == 0 ? count - 1 : count;
    }

    private static object CoerceEllipsisStep(DependencyObject d, object value)
    {
        return Math.Max((int)value, 0);
    }

    private static object CoerceCurrentPage(DependencyObject d, object value)
    {
        return Math.Max((int)value, 1);
    }

    private static object CoercePageCount(DependencyObject d, object value)
    {
        return Math.Max((int)value, 0);
    }

    private static object CoercePageSize(DependencyObject d, object value)
    {
        return Clamp((int)value, 1, ((PagingBox)d).MaximumPageSize);
    }

    private static void OnMaximumPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PagingBox)d;
        control.CoerceValue(PageSizeProperty);
        control.SynchronizePageSizeSelector();
    }

    private static void OnPagingStateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PagingBox)d;
        control.UpdatePagingState();

        if (UIElementAutomationPeer.FromElement(control) is not PagingBoxAutomationPeer peer)
        {
            return;
        }

        if (e.Property == CurrentPageProperty)
        {
            peer.RaiseCurrentPageChanged((int)e.OldValue, (int)e.NewValue);
        }
        else if (e.Property == PageCountProperty)
        {
            peer.RaisePageCountChanged((int)e.OldValue, (int)e.NewValue, control.CurrentPage);
        }
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PagingBox)d;

        if (e.OldValue is ICommand oldCommand)
        {
            CanExecuteChangedEventManager.RemoveHandler(oldCommand, control.OnCanExecuteChanged);
        }

        if (e.NewValue is ICommand newCommand)
        {
            CanExecuteChangedEventManager.AddHandler(newCommand, control.OnCanExecuteChanged);
        }

        control.UpdatePagingState();
    }

    private static void OnCommandTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PagingBox)d).UpdatePagingState();
    }

    private static void OnPageListParameterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PagingBox)d).UpdatePageList();
    }

    private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PagingBox)d).SynchronizePageSizeSelector();
    }

    private static void OnPageSizeOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (PagingBox)d;

        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            CollectionChangedEventManager.RemoveHandler(
                oldCollection,
                control.OnPageSizeOptionsCollectionChanged);
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            CollectionChangedEventManager.AddHandler(
                newCollection,
                control.OnPageSizeOptionsCollectionChanged);
        }

        control.SynchronizePageSizeSelector();
        control.UpdatePagingState();
    }

    private void AttachTemplatePartHandlers()
    {
        if (_previousButton is not null)
        {
            _previousButton.Click += OnPreviousButtonClick;
        }

        if (_nextButton is not null)
        {
            _nextButton.Click += OnNextButtonClick;
        }

        if (_currentPageTextBox is not null)
        {
            _currentPageTextBox.PreviewKeyDown += OnCurrentPageTextBoxPreviewKeyDown;
            _currentPageTextBox.LostKeyboardFocus += OnCurrentPageTextBoxLostKeyboardFocus;
        }

        if (_pageItemsControl is not null)
        {
            _pageItemsControl.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPageItemClick));
        }

        if (_pageSizeComboBox is not null)
        {
            _pageSizeComboBox.SelectionChanged += OnPageSizeComboBoxSelectionChanged;
            _pageSizeComboBox.PreviewKeyDown += OnPageSizeComboBoxPreviewKeyDown;
            _pageSizeComboBox.LostKeyboardFocus += OnPageSizeComboBoxLostKeyboardFocus;
        }
    }

    private void DetachTemplatePartHandlers()
    {
        if (_previousButton is not null)
        {
            _previousButton.Click -= OnPreviousButtonClick;
        }

        if (_nextButton is not null)
        {
            _nextButton.Click -= OnNextButtonClick;
        }

        if (_currentPageTextBox is not null)
        {
            _currentPageTextBox.PreviewKeyDown -= OnCurrentPageTextBoxPreviewKeyDown;
            _currentPageTextBox.LostKeyboardFocus -= OnCurrentPageTextBoxLostKeyboardFocus;
        }

        if (_pageItemsControl is not null)
        {
            _pageItemsControl.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnPageItemClick));
        }

        if (_pageSizeComboBox is not null)
        {
            _pageSizeComboBox.SelectionChanged -= OnPageSizeComboBoxSelectionChanged;
            _pageSizeComboBox.PreviewKeyDown -= OnPageSizeComboBoxPreviewKeyDown;
            _pageSizeComboBox.LostKeyboardFocus -= OnPageSizeComboBoxLostKeyboardFocus;
        }

        _previousButton = null;
        _nextButton = null;
        _currentPageTextBox = null;
        _pageItemsControl = null;
        _pageSizeComboBox = null;
    }

    private void UpdatePagingState()
    {
        var hasPages = PageCount > 0;

        SetValue(HasPagesPropertyKey, hasPages);
        SetValue(
            CanGoPreviousPropertyKey,
            hasPages && CurrentPage > 1 && CanExecuteCommand(CurrentPage - 1));
        SetValue(
            CanGoNextPropertyKey,
            hasPages && CurrentPage < PageCount && CanExecuteCommand(CurrentPage + 1));
        SetValue(CanChangePageSizePropertyKey, CanExecuteCommand(1));

        if (_userActionDepth == 0)
        {
            SetValue(CommandParameterPropertyKey, CurrentPage);
        }

        SynchronizeCurrentPageText();
        UpdatePageList();
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        UpdatePagingState();
    }

    private bool CanExecuteCommand(int requestedPage)
    {
        return Command switch
        {
            null                  => true,
            RoutedCommand routed => routed.CanExecute(requestedPage, CommandTarget ?? this),
            _                     => Command.CanExecute(requestedPage)
        };
    }

    private void ExecuteCommand(int requestedPage)
    {
        switch (Command)
        {
            case RoutedCommand routed when routed.CanExecute(requestedPage, CommandTarget ?? this):
                routed.Execute(requestedPage, CommandTarget ?? this);
                break;

            case { } command when command.CanExecute(requestedPage):
                command.Execute(requestedPage);
                break;
        }
    }

    private void OnPageSizeOptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizePageSizeSelector();
    }

    private void SynchronizeCurrentPageText()
    {
#pragma warning disable WPF0041 // The Text property of the internal TextBox component is not public,
        // so SetCurrentValue is not required
        _currentPageTextBox?.Text = CurrentPage.ToString();

#pragma warning restore WPF0041
    }

    private void SynchronizePageSizeSelector()
    {
        if (_pageSizeComboBox is null)
        {
            return;
        }

        _isSynchronizingPageSize = true;
        try
        {
            // A custom template only needs to provide the contracted part; it does not need to reimplement
            // synchronization between the options and the current value.
            _pageSizeComboBox.SetCurrentValue(ItemsControl.ItemsSourceProperty, PageSizeOptions);

#pragma warning disable WPF0041

            _pageSizeComboBox.SelectedItem = _pageSizeComboBox.Items
                .Cast<object>()
                .FirstOrDefault(
                    item => TryGetValidPageSize(item, out var size) && size == PageSize
                );

            _pageSizeComboBox.Text = PageSize.ToString(CultureInfo.CurrentCulture);

#pragma warning restore WPF0041
        }
        finally
        {
            _isSynchronizingPageSize = false;
        }
    }

    private void UpdatePageList()
    {
        var newItems = GeneratePageItems();
        if (!PageList.SequenceEqual(newItems, PagingItemComparer.Instance))
        {
            SetValue(PageListPropertyKey, newItems);
        }
    }

    private IReadOnlyList<PagingItem> GeneratePageItems()
    {
        if (PageCount < 1)
        {
            return [];
        }

        var visible = MaxVisiblePages;
        var result = new List<PagingItem>(Math.Min(visible, PageCount));
        var currentPageIsValid = CurrentPage <= PageCount;

        if (PageCount <= visible)
        {
            for (var page = 1; page <= PageCount; page++)
            {
                result.Add(CreatePageItem(page, currentPageIsValid));
            }

            return result;
        }

        var offset = (visible - 1) / 2;
        var step = EllipsisStep > 0 ? EllipsisStep : Math.Max(1, visible - 2);

        // 当前页在前面连续页范围内，显示前面连续页 + 后续省略号 + 最后一页
        if (CurrentPage <= offset + 1)
        {
            for (var page = 1; page <= visible - 2; page++)
            {
                result.Add(CreatePageItem(page, currentPageIsValid));
            }

            var lastContinuous = visible - 2;
            var rightTarget = ClampInnerPage((long)CurrentPage + step);
            if (rightTarget <= lastContinuous)
            {
                rightTarget = Math.Min(PageCount - 1, lastContinuous + 2);
            }

            result.Add(CreateEllipsisItem(rightTarget, PagingItemState.NextEllipsis));
            result.Add(CreatePageItem(PageCount, currentPageIsValid));
        }
        else if (CurrentPage >= PageCount - offset)
        {
            // 当前页在后面连续页范围内，显示第一页 + 前省略号 + 后续连续页

            result.Add(CreatePageItem(1, currentPageIsValid));

            var firstContinuous = PageCount - (visible - 3);
            var leftTarget = ClampInnerPage((long)CurrentPage - step);
            if (leftTarget >= firstContinuous)
            {
                leftTarget = Math.Max(2, firstContinuous - 2);
            }

            result.Add(CreateEllipsisItem(leftTarget, PagingItemState.PreviousEllipsis));
            for (var page = firstContinuous; page <= PageCount; page++)
            {
                result.Add(CreatePageItem(page, currentPageIsValid));
            }
        }
        else // 当前页在中间连续页范围内，显示第一页 + 前省略号 + 中间连续页 + 后省略号 + 最后一页
        {
            result.Add(CreatePageItem(1, currentPageIsValid));
            result.Add(CreateEllipsisItem(
                ClampInnerPage((long)CurrentPage - step),
                PagingItemState.PreviousEllipsis));

            for (var page = CurrentPage - offset + 2; page <= CurrentPage + offset - 2; page++)
            {
                result.Add(CreatePageItem(page, currentPageIsValid));
            }

            result.Add(CreateEllipsisItem(
                ClampInnerPage((long)CurrentPage + step),
                PagingItemState.NextEllipsis));

            result.Add(CreatePageItem(PageCount, currentPageIsValid));
        }

        return result;
    }

    private PagingItem CreatePageItem(int page, bool currentPageIsValid)
    {
        var state = page switch
        {
            1 when PageCount == 1    => PagingItemState.FirstAndLast,
            1                        => PagingItemState.First,
            _ when page == PageCount => PagingItemState.Last,
            _                        => PagingItemState.Normal
        };

        var isCurrent = currentPageIsValid && page == CurrentPage;
        var format = isCurrent ? CurrentPageItemAutomationNameFormat : PageItemAutomationNameFormat;
        return new PagingItem(
            page,
            state,
            isCurrent,
            !isCurrent && CanExecuteCommand(page),
            FormatPageDescription(format, page)
        );
    }

    private PagingItem CreateEllipsisItem(int page, PagingItemState state)
    {
        var format = state == PagingItemState.PreviousEllipsis
            ? PreviousEllipsisAutomationNameFormat
            : NextEllipsisAutomationNameFormat;

        return new PagingItem(
            page,
            state,
            false,
            CanExecuteCommand(page),
            FormatPageDescription(format, page)
        );
    }

    private static string FormatPageDescription(string format, int page)
    {
        return string.Format(CultureInfo.CurrentUICulture, format, page);
    }

    private int ClampInnerPage(long page)
    {
        return (int)Clamp(page, 2L, PageCount - 1L);
    }

    private void OnPreviousButtonClick(object sender, RoutedEventArgs e)
    {
        if (CanGoPrevious)
        {
            RequestPage(Math.Min(CurrentPage - 1, PageCount));
        }
    }

    private void OnNextButtonClick(object sender, RoutedEventArgs e)
    {
        if (CanGoNext)
        {
            RequestPage(CurrentPage + 1);
        }
    }

    private void OnPageItemClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is ButtonBase { DataContext: PagingItem item })
        {
            RequestPage(item.Page);
            e.Handled = true;
        }
    }

    private void OnCurrentPageTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Handled || _currentPageTextBox is null)
        {
            return;
        }

        if (int.TryParse(_currentPageTextBox.Text, out var page))
        {
            RequestPage(page);
        }

        SynchronizeCurrentPageText();
        e.Handled = true;
    }

    private void OnCurrentPageTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SynchronizeCurrentPageText();
    }

    private void OnPageSizeComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingPageSize
            || !IsLoaded
            || _pageSizeComboBox?.SelectedItem is not { } selectedItem
            || !TryGetValidPageSize(selectedItem, out var newPageSize))
        {
            if (!_isSynchronizingPageSize)
            {
                SynchronizePageSizeSelector();
            }

            return;
        }

        CommitPageSize(newPageSize);
    }

    private void OnPageSizeComboBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.Handled || !IsPageSizeEditable || _pageSizeComboBox is null)
        {
            return;
        }

        CommitPageSizeText(_pageSizeComboBox.Text);
        e.Handled = true;
    }

    private void OnPageSizeComboBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsPageSizeEditable || _pageSizeComboBox is null)
        {
            return;
        }

        var comboBox = _pageSizeComboBox;
        comboBox.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                // Wait until the drop-down completes its selection or closes so a transient focus move into the
                // Popup does not commit the value twice.
                if (!ReferenceEquals(_pageSizeComboBox, comboBox) ||
                    comboBox.IsKeyboardFocusWithin ||
                    comboBox.IsDropDownOpen)
                {
                    return;
                }

                CommitPageSizeText(comboBox.Text);
            }
        );
    }

    private void CommitPageSizeText(string text)
    {
        if (_pageSizeComboBox is null
            || !TryGetValidPageSize(text, out var newPageSize))
        {
            SynchronizePageSizeSelector();
            return;
        }

        CommitPageSize(newPageSize);
    }

    private void CommitPageSize(int newPageSize)
    {
        if (newPageSize == PageSize)
        {
            SynchronizePageSizeSelector();
            return;
        }

        if (!IsLoaded || !CanExecuteCommand(1))
        {
            SynchronizePageSizeSelector();
            return;
        }

        var oldPageSize = PageSize;
        SetCurrentValue(PageSizeProperty, newPageSize);
        ExecuteUserAction(
            1,
            () => OnPageSizeChanged(
                new RoutedPropertyChangedEventArgs<int>(oldPageSize, newPageSize, PageSizeChangedEvent)));
    }

    internal bool CanRequestPage(int page)
    {
        return IsEnabled
               && PageCount > 0
               && page >= 1
               && page <= PageCount
               && page != CurrentPage
               && CanExecuteCommand(page);
    }

    internal void RequestPage(int page)
    {
        if (!CanRequestPage(page))
        {
            return;
        }

        ExecuteUserAction(
            page,
            () => OnPageRequested(new PageRequestedEventArgs(PageRequestedEvent, page))
        );
    }

    private void ExecuteUserAction(int requestedPage, Action raiseUserEvent)
    {
        var previousParameter = CommandParameter;
        _userActionDepth++;
        SetValue(CommandParameterPropertyKey, requestedPage);

        try
        {
            // Match ButtonBase semantics by raising the user-interaction event before executing the external command.
            raiseUserEvent();

            // Use the local request snapshot so an event handler that synchronously updates CurrentPage cannot alter
            // the command parameter for this request.
            ExecuteCommand(requestedPage);
        }
        finally
        {
            _userActionDepth--;

            SetValue(
                CommandParameterPropertyKey,
                _userActionDepth == 0 ? CurrentPage : previousParameter
            );

            if (_userActionDepth == 0)
            {
                UpdatePagingState();
            }
        }
    }

    private bool TryGetValidPageSize(object value, out int result)
    {
        var candidate = value is ComboBoxItem item ? item.Content : value;
        return int.TryParse(
                candidate?.ToString(),
                NumberStyles.Integer,
                CultureInfo.CurrentCulture,
                out result) && result >= 1
                            && result <= MaximumPageSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Clamp<T>(T value, T min, T max)
        where T : IComparable<T>
    {
        if (min.CompareTo(max) > 0)
        {
            throw new ArgumentException("min must be less than or equal to max.");
        }

        if (value.CompareTo(min) < 0)
        {
            return min;
        }

        if (value.CompareTo(max) > 0)
        {
            return max;
        }

        return value;
    }

    private sealed class PagingItemComparer : IEqualityComparer<PagingItem>
    {
        public static PagingItemComparer Instance { get; } = new();

        public bool Equals(PagingItem? x, PagingItem? y)
        {
            return ReferenceEquals(x, y)
                   || (x is not null
                       && y is not null
                       && x.Page == y.Page
                       && x.State == y.State
                       && x.IsCurrent == y.IsCurrent
                       && x.IsEnabled == y.IsEnabled
                       && x.Description == y.Description);
        }

        public int GetHashCode(PagingItem obj)
        {
            return (
                obj.Page,
                obj.State,
                obj.IsCurrent,
                obj.IsEnabled,
                obj.Description).GetHashCode();
        }
    }
}