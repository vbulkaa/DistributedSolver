using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DistributedSolver.Core.Utils;

/// <summary>
/// Вспомогательные методы для сжатия JSON-пayload перед отправкой по HTTP
/// </summary>
public static class HttpCompressionHelper
{
    public enum CompressionAlgorithm
    {
        Brotli,
        GZip
    }

    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Сериализует объект в JSON и сжимает его указанным алгоритмом
    /// </summary>
    public static byte[] SerializeToCompressedJson<T>(
        T value,
        JsonSerializerOptions? options = null,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        options ??= DefaultOptions;

        using var buffer = new MemoryStream();
        using (var compressionStream = CreateCompressionStream(buffer, algorithm, level))
        {
            JsonSerializer.Serialize(compressionStream, value, options);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Сериализует произвольный бинарный payload и сжимает его
    /// </summary>
    public static byte[] SerializeToCompressedBinary(
        Action<Stream> writeAction,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli,
        CompressionLevel level = CompressionLevel.Fastest)
    {
        using var buffer = new MemoryStream();
        using (var compressionStream = CreateCompressionStream(buffer, algorithm, level))
        {
            writeAction(compressionStream);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Создаёт HttpContent из уже сжатого payload
    /// </summary>
    public static ByteArrayContent CreateCompressedContent(
        byte[] payload,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli,
        string contentType = "application/json")
    {
        var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Headers.ContentEncoding.Add(algorithm == CompressionAlgorithm.Brotli ? "br" : "gzip");
        return content;
    }

    private static Stream CreateCompressionStream(Stream output, CompressionAlgorithm algorithm, CompressionLevel level)
    {
        return algorithm switch
        {
            CompressionAlgorithm.GZip => new GZipStream(output, level, leaveOpen: true),
            _ => new BrotliStream(output, level, leaveOpen: true)
        };
    }
}


