// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Supplies a conservative Emoji catalogue whose base glyphs are supported across Windows 10 and 11.
/// </summary>
/// <remarks>
/// The catalogue intentionally contains no persisted recents or telemetry. Applications that need a
/// larger, domain-specific catalogue can replace the keyboard template and bind their own collection.
/// Keeping this list conservative avoids exposing tofu glyphs on older Windows 10 Segoe UI Emoji fonts.
/// </remarks>
internal static class SoftKeyboardEmojiCatalog
{
    internal static IReadOnlyList<SoftKeyboardEmoji> Items { get; } =
    [
        new("😀", "grinning face", SoftKeyboardEmojiCategory.Smileys),
        new("😂", "face with tears of joy", SoftKeyboardEmojiCategory.Smileys),
        new("😊", "smiling face", SoftKeyboardEmojiCategory.Smileys),
        new("😍", "smiling face with heart eyes", SoftKeyboardEmojiCategory.Smileys),
        new("😎", "smiling face with sunglasses", SoftKeyboardEmojiCategory.Smileys),
        new("🤔", "thinking face", SoftKeyboardEmojiCategory.Smileys),
        new("😢", "crying face", SoftKeyboardEmojiCategory.Smileys),
        new("😡", "angry face", SoftKeyboardEmojiCategory.Smileys),
        new("👍", "thumbs up", SoftKeyboardEmojiCategory.People, ["👍🏻", "👍🏼", "👍🏽", "👍🏾", "👍🏿"]),
        new("👎", "thumbs down", SoftKeyboardEmojiCategory.People, ["👎🏻", "👎🏼", "👎🏽", "👎🏾", "👎🏿"]),
        new("👏", "clapping hands", SoftKeyboardEmojiCategory.People, ["👏🏻", "👏🏼", "👏🏽", "👏🏾", "👏🏿"]),
        new("🙏", "folded hands", SoftKeyboardEmojiCategory.People, ["🙏🏻", "🙏🏼", "🙏🏽", "🙏🏾", "🙏🏿"]),
        new("👋", "waving hand", SoftKeyboardEmojiCategory.People, ["👋🏻", "👋🏼", "👋🏽", "👋🏾", "👋🏿"]),
        new("🐶", "dog", SoftKeyboardEmojiCategory.Animals),
        new("🐱", "cat", SoftKeyboardEmojiCategory.Animals),
        new("🐼", "panda", SoftKeyboardEmojiCategory.Animals),
        new("🌸", "cherry blossom", SoftKeyboardEmojiCategory.Animals),
        new("🍎", "red apple", SoftKeyboardEmojiCategory.Food),
        new("🍕", "pizza", SoftKeyboardEmojiCategory.Food),
        new("🍰", "cake", SoftKeyboardEmojiCategory.Food),
        new("☕", "hot beverage", SoftKeyboardEmojiCategory.Food),
        new("⚽", "soccer ball", SoftKeyboardEmojiCategory.Activities),
        new("🎉", "party popper", SoftKeyboardEmojiCategory.Activities),
        new("🎵", "musical note", SoftKeyboardEmojiCategory.Activities),
        new("🚗", "car", SoftKeyboardEmojiCategory.Travel),
        new("✈", "airplane", SoftKeyboardEmojiCategory.Travel),
        new("🏠", "house", SoftKeyboardEmojiCategory.Travel),
        new("💡", "light bulb", SoftKeyboardEmojiCategory.Objects),
        new("📱", "mobile phone", SoftKeyboardEmojiCategory.Objects),
        new("💻", "laptop", SoftKeyboardEmojiCategory.Objects),
        new("❤", "red heart", SoftKeyboardEmojiCategory.Symbols),
        new("✅", "check mark", SoftKeyboardEmojiCategory.Symbols),
        new("⭐", "star", SoftKeyboardEmojiCategory.Symbols),
        new("⚠", "warning", SoftKeyboardEmojiCategory.Symbols),
    ];
}
