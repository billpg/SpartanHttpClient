using System;
using System.Collections.Generic;

namespace billpg.SpartanHttpClient.Internal;

/// <summary>
/// Accumulates a status line and headers from one HTTP/1.1 response, fed in one line at
/// a time as they're read off the wire. Immutable - each WithResponseLine call returns
/// a new state. The body isn't handled here: once headers are Done, its framing
/// (chunked, Content-Length, or close-delimited) is decided by ResponseBodyReader.
/// </summary>
internal sealed record ResponseParseState(
    int StatusCode,
    IReadOnlyDictionary<string, string> Headers,
    ParseStage Stage,
    string? LastHeaderName)
{
    internal static readonly ResponseParseState Initial =
        new(0, HeaderMerge.Empty, ParseStage.Banner, null);
}

internal enum ParseStage { Banner, Headers, Done }

internal static class ResponseLineParser
{
    internal static ResponseParseState WithResponseLine(this ResponseParseState state, string line)
        => state.Stage == ParseStage.Banner
            ? WithBannerLine(state, line)
            : WithHeaderLine(state, line);

    /// <summary>"HTTP/1.1 200 OK" - anything else is a protocol violation, not a
    /// heuristic fallback, since a compliant server always starts with this line.</summary>
    private static ResponseParseState WithBannerLine(ResponseParseState state, string line)
    {
        var parts = line.Split(new[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[0].StartsWith("HTTP/", StringComparison.Ordinal) ||
            !int.TryParse(parts[1], out int statusCode))
            throw new SpartanHttpException(
                "External URL not available.",
                $"Malformed HTTP response status line: \"{line}\".");

        return state with { StatusCode = statusCode, Stage = ParseStage.Headers };
    }

    private static ResponseParseState WithHeaderLine(ResponseParseState state, string line)
    {
        /* A blank line ends the header block. */
        if (string.IsNullOrEmpty(line))
            return state with { Stage = ParseStage.Done };

        /* A line starting with whitespace is a folded continuation of the previous
         * header (RFC 7230-obsoleted but still seen in the wild). */
        if (char.IsWhiteSpace(line[0]))
        {
            if (state.LastHeaderName == null || !state.Headers.ContainsKey(state.LastHeaderName))
                throw new SpartanHttpException(
                    "External URL not available.",
                    "Malformed HTTP response: a header continuation line with no preceding header.");

            var folded = HeaderMerge.Copy(state.Headers);
            folded[state.LastHeaderName] = folded[state.LastHeaderName] + " " + line.Trim();
            return state with { Headers = folded };
        }

        /* "Name: value". */
        int colonIndex = line.IndexOf(':');
        if (colonIndex <= 0)
            throw new SpartanHttpException(
                "External URL not available.",
                $"Malformed HTTP response header line: \"{line}\".");
        var name = line.Substring(0, colonIndex).Trim();
        var value = line.Substring(colonIndex + 1).Trim();
        return state with { Headers = HeaderMerge.Add(state.Headers, name, value), LastHeaderName = name };
    }
}
