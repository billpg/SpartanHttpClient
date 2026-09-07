using System.Collections.Generic;
using System.Net;
using billpg.SpartanHttpClient.Internal;

namespace billpg.SpartanHttpClient;

/// <summary>A completed HTTP response, as returned by SpartanRequest.Run().</summary>
public sealed record SpartanResponse
{
    public int StatusCode { get; internal init; }
    public IReadOnlyDictionary<string, string> Headers { get; internal init; } = HeaderMerge.Empty;
    public string Body { get; internal init; } = "";

    /// <summary>The IP address the request was actually sent to, once resolved and
    /// accepted by the configured IsIpAddressAcceptableDelegate.</summary>
    public IPAddress? RemoteAddress { get; internal init; }

    /// <summary>The remote's TLS certificate, as a Base64-encoded SHA-256 hash. Null
    /// for a plain HTTP connection.</summary>
    public string? RemoteCertificateHash { get; internal init; }
}
