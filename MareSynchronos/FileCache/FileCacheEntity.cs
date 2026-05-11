#nullable disable

namespace MareSynchronos.FileCache;

/// <summary>
/// Represents a cached file entity containing metadata, pathing information, and size details.
/// </summary>
public class FileCacheEntity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileCacheEntity"/> class.
    /// </summary>
    /// <param name="hash">The unique file hash identifier.</param>
    /// <param name="path">The prefixed path to the file.</param>
    /// <param name="lastModifiedDateTicks">The last modified timestamp in ticks format.</param>
    /// <param name="size">The uncompressed size of the file in bytes.</param>
    /// <param name="compressedSize">The compressed size of the file in bytes.</param>
    public FileCacheEntity(string hash, string path, string lastModifiedDateTicks, long? size = null, long? compressedSize = null)
    {
        Size = size;
        CompressedSize = compressedSize;
        Hash = hash;
        PrefixedFilePath = path;
        LastModifiedDateTicks = lastModifiedDateTicks;
    }

    /// <summary>
    /// Gets or sets the size of the file after compression.
    /// </summary>
    public long? CompressedSize { get; set; }

    /// <summary>
    /// Gets a formatted CSV string representation of the file cache entry for storage.
    /// </summary>
    public string CsvEntry => $"{Hash}{FileCacheManager.CsvSplit}{PrefixedFilePath}{FileCacheManager.CsvSplit}{LastModifiedDateTicks}|{Size ?? -1}|{CompressedSize ?? -1}";

    /// <summary>
    /// Gets or sets the unique file hash.
    /// </summary>
    public string Hash { get; set; }

    /// <summary>
    /// Gets a value indicating whether the file path starts with the designated cache prefix.
    /// </summary>
    public bool IsCacheEntry => PrefixedFilePath.StartsWith(FileCacheManager.CachePrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the last modified date of the file represented as ticks.
    /// </summary>
    public string LastModifiedDateTicks { get; set; }

    /// <summary>
    /// Gets the prefixed file path string.
    /// </summary>
    public string PrefixedFilePath { get; init; }

    /// <summary>
    /// Gets the fully resolved and sanitized file path.
    /// </summary>
    public string ResolvedFilepath { get; private set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uncompressed size of the file.
    /// </summary>
    public long? Size { get; set; }

    /// <summary>
    /// Normalizes and sets the <see cref="ResolvedFilepath"/> from a raw file path string.
    /// </summary>
    /// <param name="filePath">The raw file path to resolve.</param>
    public void SetResolvedFilePath(string filePath)
    {
        ResolvedFilepath = filePath.ToLowerInvariant().Replace("\\\\", "\\", StringComparison.Ordinal);
    }
}