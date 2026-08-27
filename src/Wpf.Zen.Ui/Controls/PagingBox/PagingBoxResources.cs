using System.Resources;

namespace Wpf.Zen.Ui.Controls
{
    /// <summary>
    /// Resolves the localized interface text used by <see cref="PagingBox"/>.
    /// </summary>
    internal static class PagingBoxResources
    {
        private const string ResourceBaseName =
            "Wpf.Zen.Ui.Controls.PagingBox.PagingBoxStrings";

        private static readonly ResourceManager ResourceManager = new(
            ResourceBaseName,
            typeof(PagingBoxResources).Assembly
        );

        internal static string Pagination => GetString(nameof(Pagination));

        internal static string PageSizeLabel => GetString(nameof(PageSizeLabel));

        internal static string PreviousPage => GetString(nameof(PreviousPage));

        internal static string NextPage => GetString(nameof(NextPage));

        internal static string PageSizeSelectorAutomationName => GetString(nameof(PageSizeSelectorAutomationName));

        internal static string CurrentPageInputAutomationName => GetString(nameof(CurrentPageInputAutomationName));

        internal static string CurrentPageInputToolTip => GetString(nameof(CurrentPageInputToolTip));

        internal static string PageItemAutomationNameFormat => GetString(nameof(PageItemAutomationNameFormat));

        internal static string CurrentPageItemAutomationNameFormat => GetString(nameof(CurrentPageItemAutomationNameFormat));

        internal static string PreviousEllipsisAutomationNameFormat => GetString(nameof(PreviousEllipsisAutomationNameFormat));

        internal static string NextEllipsisAutomationNameFormat => GetString(nameof(NextEllipsisAutomationNameFormat));

        internal static string PageRequestNotAllowed => GetString(nameof(PageRequestNotAllowed));

        private static string GetString(string name)
        {
            return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
        }
    }
}
