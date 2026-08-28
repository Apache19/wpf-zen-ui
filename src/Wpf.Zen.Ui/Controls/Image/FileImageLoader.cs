// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.IO;
using System.Windows.Media.Imaging;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Implements the file-specific decode path used by <see cref="Image.FileSource"/>.
/// </summary>
internal static class FileImageLoader
{
    private const int CacheTrimThreshold = 128;

    private static readonly object CacheLock = new();
    private static readonly Dictionary<FileImageCacheKey, WeakReference<BitmapSource>> Cache = [];

    /// <summary>
    /// Resolves a file source or throws when the value does not describe a local file.
    /// </summary>
    internal static string ResolveFilePath(
        object value,
        Uri? baseUri,
        bool resolveRelativeAgainstCurrentDirectory
    )
    {
        if (
            TryResolveFilePath(
                value,
                baseUri,
                resolveRelativeAgainstCurrentDirectory,
                out var filePath
            )
        )
        {
            return filePath!;
        }

        throw new NotSupportedException($"'{value}' does not identify a local image file.");
    }

    /// <summary>
    /// Resolves absolute paths and file URIs without interpreting pack or network URIs as files.
    /// </summary>
    internal static bool TryResolveFilePath(
        object value,
        Uri? baseUri,
        bool resolveRelativeAgainstCurrentDirectory,
        out string? filePath
    )
    {
        filePath = null;

        var uri = value as Uri;
        var text = value as string;
        if (uri is null && string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (uri is null)
        {
            // Path.IsPathRooted must run before URI parsing. On .NET Framework a drive-qualified
            // Windows path can otherwise be interpreted as an unsupported one-letter URI scheme.
            if (Path.IsPathRooted(text!))
            {
                filePath = Path.GetFullPath(text!);
                return true;
            }

            if (Uri.TryCreate(text, UriKind.Absolute, out var absoluteUri))
            {
                uri = absoluteUri;
            }
            else
            {
                uri = new Uri(text!, UriKind.Relative);
            }
        }

        if (uri.IsAbsoluteUri)
        {
            if (!uri.IsFile)
            {
                return false;
            }

            filePath = Path.GetFullPath(uri.LocalPath);
            return true;
        }

        if (baseUri is { IsAbsoluteUri: true, IsFile: true })
        {
            filePath = Path.GetFullPath(new Uri(baseUri, uri).LocalPath);
            return true;
        }

        if (!resolveRelativeAgainstCurrentDirectory)
        {
            return false;
        }

        filePath = Path.GetFullPath(uri.OriginalString);
        return true;
    }

    /// <summary>
    /// Returns a frozen decoded bitmap, reusing a live bitmap when the same unchanged file and
    /// decode dimensions were loaded previously.
    /// </summary>
    internal static BitmapSource Load(string filePath, int decodePixelWidth, int decodePixelHeight)
    {
        var fileInfo = new FileInfo(filePath);
        var cacheKey = new FileImageCacheKey(
            fileInfo.FullName,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks,
            decodePixelWidth,
            decodePixelHeight
        );

        lock (CacheLock)
        {
            if (
                Cache.TryGetValue(cacheKey, out var reference)
                && reference.TryGetTarget(out var cachedBitmap)
            )
            {
                return cachedBitmap;
            }
        }

        var bitmap = Decode(cacheKey.FilePath, decodePixelWidth, decodePixelHeight);

        lock (CacheLock)
        {
            if (Cache.Count >= CacheTrimThreshold)
            {
                TrimCache();
            }

            Cache[cacheKey] = new WeakReference<BitmapSource>(bitmap);
        }

        return bitmap;
    }

    /// <summary>
    /// Decodes directly from the file stream. OnLoad transfers all WIC pixel dependencies into the
    /// bitmap before the using scope ends; SequentialScan avoids unnecessary random-read hints, and
    /// FileShare.Delete permits atomic replacement workflows even during the short decode window.
    /// </summary>
    private static BitmapSource Decode(
        string filePath,
        int decodePixelWidth,
        int decodePixelHeight
    )
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            4096,
            FileOptions.SequentialScan
        );
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.DecodePixelHeight = decodePixelHeight;
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        // A frozen BitmapSource drops dispatcher affinity, removes Freezable change tracking, and
        // is therefore safe to share through the weak cache across controls and UI dispatchers.
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>
    /// Removes dead weak entries. Trimming is opportunistic so ordinary loads allocate no cleanup
    /// work and the cache never keeps decoded pixel buffers alive by itself.
    /// </summary>
    private static void TrimCache()
    {
        var keysToRemove = new List<FileImageCacheKey>();
        foreach (var entry in Cache)
        {
            if (!entry.Value.TryGetTarget(out _))
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            Cache.Remove(key);
        }
    }

    private readonly struct FileImageCacheKey : IEquatable<FileImageCacheKey>
    {
        internal FileImageCacheKey(
            string filePath,
            long fileLength,
            long lastWriteTicks,
            int decodePixelWidth,
            int decodePixelHeight
        )
        {
            FilePath = filePath;
            FileLength = fileLength;
            LastWriteTicks = lastWriteTicks;
            DecodePixelWidth = decodePixelWidth;
            DecodePixelHeight = decodePixelHeight;
        }

        internal string FilePath { get; }

        private long FileLength { get; }

        private long LastWriteTicks { get; }

        private int DecodePixelWidth { get; }

        private int DecodePixelHeight { get; }

        public bool Equals(FileImageCacheKey other) =>
            StringComparer.OrdinalIgnoreCase.Equals(FilePath, other.FilePath)
            && FileLength == other.FileLength
            && LastWriteTicks == other.LastWriteTicks
            && DecodePixelWidth == other.DecodePixelWidth
            && DecodePixelHeight == other.DecodePixelHeight;

        public override bool Equals(object? obj) => obj is FileImageCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath);
                hashCode = (hashCode * 397) ^ FileLength.GetHashCode();
                hashCode = (hashCode * 397) ^ LastWriteTicks.GetHashCode();
                hashCode = (hashCode * 397) ^ DecodePixelWidth;
                return (hashCode * 397) ^ DecodePixelHeight;
            }
        }
    }
}