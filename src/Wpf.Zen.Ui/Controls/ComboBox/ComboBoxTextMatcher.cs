// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Ranks candidates without changing their order or allocating a filtered item collection.
/// </summary>
internal sealed class ComboBoxTextMatcher
{
    private readonly string                 _query;
    private readonly string[]               _tokens;
    private readonly ComboBoxTextSearchMode _mode;
    private readonly CompareInfo            _comparison;
    private readonly CompareOptions         _options;

    /// <summary>
    /// Prepares one input query using the control's language and case-sensitivity settings.
    /// </summary>
    internal ComboBoxTextMatcher(
        string query,
        ComboBoxTextSearchMode mode,
        CultureInfo culture,
        bool caseSensitive)
    {
        _query      = query;
        _tokens     = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        _mode       = mode;
        _comparison = culture.CompareInfo;
        _options    = caseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase;
    }

    /// <summary>
    /// Returns a lower rank for a stronger match, or -1 when the candidate does not match.
    /// Exact, prefix, substring, token, and subsequence matches have successive priority.
    /// Empty and whitespace-only queries deliberately select no item.
    /// </summary>
    internal int GetRank(string candidate)
    {
        if (_tokens.Length == 0)
        {
            return -1;
        }

        if (_comparison.Compare(candidate, _query, _options) == 0)
        {
            return 0;
        }

        if (_comparison.IsPrefix(candidate, _query, _options))
        {
            return 1;
        }

        if (_comparison.IndexOf(candidate, _query, _options) >= 0)
        {
            return 2;
        }

        if (_mode == ComboBoxTextSearchMode.Contains)
        {
            return -1;
        }

        var rank = 3;

        foreach (var token in _tokens)
        {
            if (_comparison.IndexOf(candidate, token, _options) >= 0)
            {
                continue;
            }

            if (_mode != ComboBoxTextSearchMode.Fuzzy || !IsSubsequence(candidate, token))
            {
                return -1;
            }

            rank = 4;
        }

        return rank;
    }

    /// <summary>
    /// Matches complete Unicode text elements in order, keeping surrogate pairs and combining
    /// sequences together. Each successive element must consume a later candidate element.
    /// </summary>
    private bool IsSubsequence(string candidate, string token)
    {
        var candidateElements = StringInfo.GetTextElementEnumerator(candidate);
        var tokenElements     = StringInfo.GetTextElementEnumerator(token);

        while (tokenElements.MoveNext())
        {
            var tokenElement = tokenElements.GetTextElement();
            var found        = false;

            while (candidateElements.MoveNext())
            {
                var candidateElement = candidateElements.GetTextElement();
                if (_comparison.Compare(candidateElement, tokenElement, _options) == 0)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}