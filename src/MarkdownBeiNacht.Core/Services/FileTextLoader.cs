using System.Text;
using MarkdownBeiNacht.Core.Models;

namespace MarkdownBeiNacht.Core.Services;

public sealed class FileTextLoader
{
    private static readonly UTF8Encoding Utf8Strict = new(false, true);
    private static readonly UnicodeEncoding Utf16LittleEndian = new(false, true, true);
    private static readonly UnicodeEncoding Utf16BigEndian = new(true, true, true);
    private static readonly UTF32Encoding Utf32LittleEndian = new(false, true, true);
    private static readonly UTF32Encoding Utf32BigEndian = new(true, true, true);

    public async Task<FileLoadResult> ReadAsync(string path, CancellationToken cancellationToken = default)
    {
        var normalizedPath = MarkdownPathUtilities.NormalizePath(path);
        if (!File.Exists(normalizedPath))
        {
            return FileLoadResult.Failure(FileLoadFailureKind.NotFound, $"The file was not found: {normalizedPath}");
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(normalizedPath, cancellationToken);
                return FileLoadResult.FromContent(Decode(bytes));
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return FileLoadResult.Failure(FileLoadFailureKind.NotFound, $"The file was not found: {normalizedPath}");
            }
            catch (DecoderFallbackException)
            {
                return FileLoadResult.Failure(
                    FileLoadFailureKind.InvalidEncoding,
                    "The file could not be decoded as UTF-8 or UTF-16 text.");
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(120 * attempt), cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return FileLoadResult.Failure(FileLoadFailureKind.ReadError, ex.Message);
            }
        }

        return FileLoadResult.Failure(
            FileLoadFailureKind.ReadError,
            "The file is temporarily unavailable because another process is still writing it.");
    }

    internal static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0xFE && bytes[3] == 0xFF)
        {
            return Utf32BigEndian.GetString(bytes, 4, bytes.Length - 4);
        }

        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return Utf32LittleEndian.GetString(bytes, 4, bytes.Length - 4);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Utf8Strict.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Utf16BigEndian.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Utf16LittleEndian.GetString(bytes, 2, bytes.Length - 2);
        }

        return Utf8Strict.GetString(bytes);
    }
}

