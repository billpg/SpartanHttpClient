using System.Collections.Generic;
using System.Net;
using billpg.SpartanHttpClient.Internal;

namespace billpg.SpartanHttpClient;

/// <summary>
/// A completed HTTP response, as returned by SpartanRequest.Run(). Immutable, in the same
/// style as SpartanRequest itself - each With... call returns a new instance. Property
/// setters stay internal (only this library ever constructs one from a real fetch), but
/// the With... methods are public so callers - test doubles especially - can build one by
/// hand, e.g. for a fake IHttpGetter-style abstraction elsewhere.
/// </summary>
public sealed record SpartanResponse
{
    public int StatusCode { get; internal init; }
    public IReadOnlyDictionary<string, string> Headers { get; internal init; } = HeaderMerge.Empty;
    public string Body { get; internal init; } = "";

    /// <summary>The IP address the request was actually sent to, once resolved and
    /// accepted by the configured IpLookupDelegate.</summary>
    public IPAddress? RemoteAddress { get; internal init; }

    /// <summary>The remote's TLS certificate, as a Base64-encoded SHA-256 hash. Null
    /// for a plain HTTP connection.</summary>
    public string? RemoteCertificateHash { get; internal init; }

    public SpartanResponse WithStatusCode(int statusCode)
        => this with { StatusCode = statusCode };

    /// <summary>Adds a header. Setting the same name twice combines the values with a
    /// comma, per RFC 9110 5.3, rather than replacing the earlier value - the same
    /// merge behavior, and the same underlying helper, as SpartanRequest.WithHeader.</summary>
    public SpartanResponse WithHeader(string name, string? value)
        => this with { Headers = HeaderMerge.Add(Headers, name, value) };

    public SpartanResponse WithBody(string body)
        => this with { Body = body };

    public SpartanResponse WithRemoteAddress(IPAddress? remoteAddress)
        => this with { RemoteAddress = remoteAddress };

    public SpartanResponse WithRemoteCertificateHash(string? hash)
        => this with { RemoteCertificateHash = hash };
}
