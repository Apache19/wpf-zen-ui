// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// TODO: This class is work in progress.
//
using System.Text.RegularExpressions;
using System.Windows.Documents;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Gallery.Controls.SyntaxHighlight;

/// <summary>
/// Formats a string of code into <see cref="System.Windows.Controls.TextBox"/> control.
/// <para>Implementation and regex patterns inspired by <see href="https://github.com/antoniandre/simple-syntax-highlighter"/>.</para>
/// </summary>
internal static class Highlighter
{
    // Each pattern below carries a named group. FormatAsParagraph decides the colour from
    // the name of the group that captured a match, so token colours never depend on
    // heuristics over token content.
    private const string EndlinePattern = /* language=regex */
        @"(?<Endline>\n)";

    private const string TabPattern = /* language=regex */
        @"(?<Tab>\t)";

    private const string QuotePattern = /* language=regex */
        @"(?<Quote>""(?:\\""|[^""])*""|'(?:\\'|[^'])*')";

    private const string CommentPattern = /* language=regex */
        @"(?<Comment>//.*?(?:\n|$)|/\*.*?\*/)";

    // Matches only the opening bracket and tag name; attributes and their values are left
    // for Quote/Plain so they can be coloured separately. The bracket and the tag name are
    // captured as distinct groups so they can take different colours.
    private const string TagPattern = /* language=regex */
        @"(?<Bracket></?)(?<Tag>[a-zA-Z][a-zA-Z0-9\-:]*)>?";

    private const string EntityPattern = /* language=regex */
        @"(?<Entity>&[a-zA-Z0-9#]+;)";

    /// <summary>Number of spaces a single tab is expanded to when rendering code samples.</summary>
    private const int TabWidth = 4;

    // Catch-all that consumes any character not claimed by a more specific pattern above,
    // so that ordinary whitespace (indentation, inter-token spaces) and plain text survive
    // tokenization instead of being dropped between matches.
    private const string PlainPattern = /* language=regex */
        @"(?<Plain>[\s\S])";

    public static Paragraph FormatAsParagraph(
        string code,
        SyntaxLanguage language = SyntaxLanguage.Autodetect
    )
    {
        var paragraph = new Paragraph();
        Regex rgx = new(GetPattern(language, code));

        bool lightTheme = IsLightTheme();

        SolidColorBrush quoteBrush = lightTheme ? Brushes.DarkCyan : Brushes.CornflowerBlue;
        SolidColorBrush tagBrush = lightTheme ? Brushes.DarkCyan : Brushes.CornflowerBlue;
        SolidColorBrush tagNameBrush = Brushes.Aqua;
        SolidColorBrush plainBrush = lightTheme ? Brushes.DarkSlateGray : Brushes.WhiteSmoke;

        // Every alternative in the pattern carries a named group and the final catch-all
        // consumes any character not claimed by a more specific one. Iterating the matches
        // therefore covers the entire input without gaps and colours each token by the name
        // of the group that produced it.
        foreach (Match match in rgx.Matches(code).Cast<Match>())
        {
            string token = match.Value;

            if (match.Groups["Endline"].Success)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
            else if (match.Groups["Tab"].Success)
            {
                paragraph.Inlines.Add(Line(new string(' ', TabWidth), Brushes.Transparent));
            }
            else if (match.Groups["Comment"].Success)
            {
                paragraph.Inlines.Add(Line(token, Brushes.Orange));
            }
            else if (match.Groups["Tag"].Success)
            {
                Group bracket = match.Groups["Bracket"];

                if (bracket.Success)
                {
                    paragraph.Inlines.Add(Line(bracket.Value, tagBrush));
                }

                paragraph.Inlines.Add(Line(match.Groups["Tag"].Value, tagNameBrush));

                // A tag immediately followed by ">" (no attributes) carries it in this match.
                if (token.EndsWith(">"))
                {
                    paragraph.Inlines.Add(Line(">", tagBrush));
                }
            }
            else if (match.Groups["Quote"].Success)
            {
                AddQuotedRun(paragraph, token, quoteBrush);
            }
            else
            {
                paragraph.Inlines.Add(Line(token, plainBrush));
            }
        }

        return paragraph;
    }

    public static string Clean(string code)
    {
        code = code.Replace(@"\n", "\n");
        code = code.Replace(@"\t", "\t");
        code = code.Replace("&lt;", "<");
        code = code.Replace("&gt;", ">");
        code = code.Replace("&amp;", "&");
        code = code.Replace("&quot;", "\"");
        code = code.Replace("&apos;", "'");

        return code;
    }

    private static Run Line(string line, SolidColorBrush brush)
    {
        return new Run(line) { Foreground = brush };
    }

    /// <summary>
    /// Renders a quoted token so the surrounding quotes take the accent colour while the
    /// inner content (an attribute or string value) is highlighted.
    /// </summary>
    private static void AddQuotedRun(Paragraph paragraph, string token, SolidColorBrush quoteBrush)
    {
        char quote = token[0];

        // A single matched string is always of the form quote + content + quote.
        paragraph.Inlines.Add(Line(quote.ToString(), quoteBrush));
        paragraph.Inlines.Add(Line(token.Substring(1, token.Length - 2), Brushes.Coral));
        paragraph.Inlines.Add(Line(quote.ToString(), quoteBrush));
    }

    private static bool IsLightTheme()
    {
        return ThemeManager.EffectiveTheme == ThemeVariant.Light;
    }

    /*
    private static string GetPattern(SyntaxLanguage language)
    {
        return GetPattern(language, string.Empty);
    }
    */

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Style",
        "IDE0060:Remove unused parameter",
        Justification = "WIP"
    )]
    private static string GetPattern(SyntaxLanguage language, string code)
    {
        var pattern = string.Empty;

        // TODO: Auto detected
        if (language == SyntaxLanguage.Autodetect)
        {
            language = SyntaxLanguage.XAML;
        }

        switch (language)
        {
            case SyntaxLanguage.CSHARP:
                pattern += EndlinePattern;
                pattern += "|" + TabPattern;
                pattern += "|" + QuotePattern;
                pattern += "|" + CommentPattern;
                pattern += "|" + EntityPattern;
                pattern += "|" + TagPattern;
                pattern += "|" + PlainPattern;
                break;

            case SyntaxLanguage.XAML:
                pattern += EndlinePattern;
                pattern += "|" + TabPattern;
                pattern += "|" + QuotePattern;
                pattern += "|" + CommentPattern;
                pattern += "|" + EntityPattern;
                pattern += "|" + TagPattern;
                pattern += "|" + PlainPattern;
                break;
        }

        return pattern;
    }
}
