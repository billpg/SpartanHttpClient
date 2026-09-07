using System.Collections.Generic;
using System.Linq;

namespace billpg.SpartanHttpClient.Internal;

internal static class HeaderMerge
{
    internal static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    /// <summary>
    /// Returns a new header dictionary with the given name/value added. A header that
    /// already exists is extended with a comma, per RFC 9110 5.3 - this is a repeated
    /// WithHeader call setting the same name, not a multi-line/folded header parsed off
    /// the wire (see ResponseLineParser for that case).
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Add(
        IReadOnlyDictionary<string, string> headers, string name, string value)
    {
        var newHeaders = Copy(headers);
        var oldValue = newHeaders.TryGetValue(name, out var existing) ? existing : null;
        newHeaders[name] = oldValue == null ? value : oldValue + "," + value;
        return newHeaders;
    }

    /// <summary>Dictionary&lt;TKey,TValue&gt; has no constructor accepting an
    /// IReadOnlyDictionary, only IDictionary, so copying needs an explicit loop.</summary>
    internal static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> headers)
        => headers.ToDictionary(kv => kv.Key, kv => kv.Value);
}
