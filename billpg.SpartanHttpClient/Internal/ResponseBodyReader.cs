using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient.Internal;

/// <summary>
/// Reads a response body according to whichever framing the headers actually declare,
/// rather than assuming the server will honor "Connection: close" or the
/// "Accept-Encoding: identity" this library sends - some servers keep the connection
/// alive regardless, or reply chunked regardless, so a raw read-until-EOF is only safe
/// as the last-resort fallback prescribed by RFC 7230 3.3.3 for HTTP/1.0-style replies.
/// </summary>
internal static class ResponseBodyReader
{
    internal static Task<string> ReadAsync(
        ResponseByteReader reader, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
    {
        if (TryGetHeader(headers, "Transfer-Encoding", out var transferEncoding) &&
            transferEncoding.IndexOf("chunked", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return ReadChunkedAsync(reader, cancellationToken);

        if (TryGetHeader(headers, "Content-Length", out var contentLengthText) &&
            long.TryParse(contentLengthText, NumberStyles.None, CultureInfo.InvariantCulture, out var contentLength) &&
            contentLength >= 0)
            return ReadFixedLengthAsync(reader, contentLength, cancellationToken);

        return ReadUntilClosedAsync(reader, cancellationToken);
    }

    private static async Task<string> ReadFixedLengthAsync(ResponseByteReader reader, long contentLength, CancellationToken cancellationToken)
    {
        /* A declared length past int.MaxValue can't be requested from ReadUpToAsync in
         * one call; reading in bounded slices handles that without changing behavior
         * for the overwhelmingly common case of an ordinary-sized body. */
        using var body = new MemoryStream();
        long remaining = contentLength;
        while (remaining > 0)
        {
            int slice = remaining > 65536 ? 65536 : (int)remaining;
            var bytes = await reader.ReadUpToAsync(slice, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
                break; /* connection closed or byte cap hit before the declared length was reached */
            body.Write(bytes, 0, bytes.Length);
            remaining -= bytes.Length;
        }
        return Encoding.ASCII.GetString(body.ToArray());
    }

    private static async Task<string> ReadChunkedAsync(ResponseByteReader reader, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        while (true)
        {
            var sizeLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (sizeLine == null)
                break; /* connection dropped mid-stream; return what was read so far */

            /* Chunk extensions ("3;foo=bar") are legal but this library has no use for
             * them - only the hex size before the semicolon matters. */
            var sizeText = sizeLine.Split(';')[0].Trim();
            if (!int.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int chunkSize) || chunkSize < 0)
                throw new SpartanHttpException(
                    "External URL not available.",
                    $"Malformed chunked response: bad chunk size \"{sizeLine}\".");

            if (chunkSize == 0)
            {
                /* Optional trailer headers, then the terminating blank line - this
                 * library has no use for trailers, only for knowing where they end. */
                string? trailerLine;
                do { trailerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false); }
                while (!string.IsNullOrEmpty(trailerLine));
                break;
            }

            var chunkData = await reader.ReadUpToAsync(chunkSize, cancellationToken).ConfigureAwait(false);
            body.Write(chunkData, 0, chunkData.Length);
            if (chunkData.Length < chunkSize)
                break; /* byte cap or closed connection cut the chunk short */

            /* Each chunk's data is followed by a CRLF before the next chunk-size line. */
            await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        return Encoding.ASCII.GetString(body.ToArray());
    }

    private static async Task<string> ReadUntilClosedAsync(ResponseByteReader reader, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        while (true)
        {
            var bytes = await reader.ReadUpToAsync(8192, cancellationToken).ConfigureAwait(false);
            if (bytes.Length == 0)
                break;
            body.Write(bytes, 0, bytes.Length);
        }
        return Encoding.ASCII.GetString(body.ToArray());
    }

    /// <summary>HTTP header names are case-insensitive (RFC 9110 5.1), but the Headers
    /// dictionary keys on whatever case the server actually sent.</summary>
    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        foreach (var kv in headers)
        {
            if (string.Equals(kv.Key, name, System.StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }
        value = "";
        return false;
    }
}
