using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient.Internal;

/// <summary>
/// Buffers bytes off the response stream one socket read at a time, offering them back
/// either as CRLF-terminated lines (for the status line and headers) or as an exact byte
/// count (for a chunk's data or a Content-Length body). A single running total across
/// both is enforced against the optional maxTotalBytes budget - carried over from the
/// original 1000-byte HashBack cap, generalized here to any configured limit - so a
/// buffered read never pulls more off the wire than the request allows, regardless of
/// how the bytes end up split between headers and body.
/// </summary>
internal sealed class ResponseByteReader
{
    private readonly Stream stream;
    private readonly long? maxTotalBytes;
    private readonly byte[] buffer = new byte[8192];
    private int bufferStart;
    private int bufferLength;

    internal long TotalConsumed { get; private set; }

    internal ResponseByteReader(Stream stream, long? maxTotalBytes)
    {
        this.stream = stream;
        this.maxTotalBytes = maxTotalBytes;
    }

    /// <summary>Reads one line, stripping the trailing CRLF (or bare LF). Returns null
    /// only when the connection ended before any bytes of a new line arrived.</summary>
    internal async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new List<byte>();
        while (true)
        {
            if (!await FillBufferAsync(cancellationToken).ConfigureAwait(false))
                return line.Count == 0 ? null : Encoding.ASCII.GetString(line.ToArray());

            byte b = buffer[bufferStart++];
            TotalConsumed++;
            if (b == (byte)'\n')
            {
                if (line.Count > 0 && line[line.Count - 1] == (byte)'\r')
                    line.RemoveAt(line.Count - 1);
                return Encoding.ASCII.GetString(line.ToArray());
            }
            line.Add(b);
        }
    }

    /// <summary>Reads up to <paramref name="count"/> bytes. Returns fewer only when the
    /// connection closed or the byte budget ran out first.</summary>
    internal async Task<byte[]> ReadUpToAsync(int count, CancellationToken cancellationToken)
    {
        var result = new byte[count];
        int filled = 0;
        while (filled < count)
        {
            if (!await FillBufferAsync(cancellationToken).ConfigureAwait(false))
                break;

            int available = bufferLength - bufferStart;
            int toCopy = Math.Min(available, count - filled);
            Array.Copy(buffer, bufferStart, result, filled, toCopy);
            bufferStart += toCopy;
            filled += toCopy;
            TotalConsumed += toCopy;
        }
        return filled == count ? result : CopyOf(result, filled);
    }

    private async Task<bool> FillBufferAsync(CancellationToken cancellationToken)
    {
        if (bufferStart < bufferLength)
            return true;

        if (maxTotalBytes != null && TotalConsumed >= maxTotalBytes)
            return false;

        int readLimit = buffer.Length;
        if (maxTotalBytes != null)
            readLimit = (int)Math.Min(readLimit, maxTotalBytes.Value - TotalConsumed);

        bufferLength = await stream.ReadAsync(buffer, 0, readLimit, cancellationToken).ConfigureAwait(false);
        bufferStart = 0;
        return bufferLength > 0;
    }

    private static byte[] CopyOf(byte[] source, int length)
    {
        var trimmed = new byte[length];
        Array.Copy(source, trimmed, length);
        return trimmed;
    }
}
