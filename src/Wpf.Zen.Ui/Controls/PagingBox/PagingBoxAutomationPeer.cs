using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides the UI Automation range-value pattern for <see cref="PagingBox"/>.
/// </summary>
internal sealed class PagingBoxAutomationPeer(PagingBox owner)
    : FrameworkElementAutomationPeer(owner), IRangeValueProvider
{
    private PagingBox PagingBoxOwner => (PagingBox)Owner;

    bool IRangeValueProvider.IsReadOnly => !PagingBoxOwner.IsEnabled || PagingBoxOwner.PageCount <= 1;

    double IRangeValueProvider.LargeChange =>
        PagingBoxOwner.EllipsisStep > 0
            ? PagingBoxOwner.EllipsisStep
            : Math.Max(1, PagingBoxOwner.MaxVisiblePages - 2);

    double IRangeValueProvider.Maximum => PagingBoxOwner.PageCount;

    double IRangeValueProvider.Minimum => PagingBoxOwner.PageCount > 0 ? 1 : 0;

    double IRangeValueProvider.SmallChange => 1;

    double IRangeValueProvider.Value =>
        PagingBoxOwner.PageCount > 0
            ? Math.Min(PagingBoxOwner.CurrentPage, PagingBoxOwner.PageCount)
            : 0;

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
    {
        return patternInterface == PatternInterface.RangeValue
            ? this
            : base.GetPattern(patternInterface);
    }

    /// <inheritdoc />
    protected override string GetClassNameCore()
    {
        return nameof(PagingBox);
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Group;
    }

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? PagingBoxResources.Pagination : name;
    }

    void IRangeValueProvider.SetValue(double value)
    {
        if (!PagingBoxOwner.IsEnabled)
            throw new ElementNotEnabledException();

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (!IsFiniteDouble(value) || value != Math.Truncate(value) ||
            value < 1 || value > PagingBoxOwner.PageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        var page = checked((int)value);
        if (page == PagingBoxOwner.CurrentPage)
            return;

        if (!PagingBoxOwner.CanRequestPage(page))
            throw new InvalidOperationException(PagingBoxResources.PageRequestNotAllowed);

        PagingBoxOwner.RequestPage(page);
        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsFiniteDouble(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            return (bits & 0x7FFFFFFFFFFFFFFF) < 0x7FF0000000000000;
        }
    }

    /// <summary>Notifies accessibility clients that the current page has changed.</summary>
    internal void RaiseCurrentPageChanged(double oldPage, double newPage)
    {
        var pageCount = PagingBoxOwner.PageCount;
        var oldValue = pageCount > 0 ? Math.Min(oldPage, pageCount) : 0d;
        var newValue = pageCount > 0 ? Math.Min(newPage, pageCount) : 0d;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (oldValue != newValue)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                oldValue,
                newValue);
        }
    }

    /// <summary>Notifies accessibility clients that the page count has changed.</summary>
    internal void RaisePageCountChanged(
        double oldPageCount,
        double newPageCount,
        double currentPage)
    {
        RaisePropertyChangedEvent(
            RangeValuePatternIdentifiers.MaximumProperty,
            oldPageCount,
            newPageCount);

        var oldMinimum = oldPageCount > 0 ? 1d : 0d;
        var newMinimum = newPageCount > 0 ? 1d : 0d;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (oldMinimum != newMinimum)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.MinimumProperty,
                oldMinimum,
                newMinimum);
        }

        var oldValue = oldPageCount > 0 ? Math.Min(currentPage, oldPageCount) : 0d;
        var newValue = newPageCount > 0 ? Math.Min(currentPage, newPageCount) : 0d;
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (oldValue != newValue)
        {
            RaisePropertyChangedEvent(
                RangeValuePatternIdentifiers.ValueProperty,
                oldValue,
                newValue);
        }
    }
}
