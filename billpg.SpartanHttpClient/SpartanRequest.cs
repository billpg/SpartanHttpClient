using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using billpg.SpartanHttpClient.Internal;

namespace billpg.SpartanHttpClient;

/// <summary>
/// An immutable HTTP GET request. Build one up with a chain of With... calls, then
/// await Run() to fetch it:
///
/// <code>
/// var response = await new SpartanRequest("https://example.com/")
///     .WithHeader("Accept", "text/plain")
///     .Run();
/// </code>
/// </summary>
public sealed record SpartanRequest
{
    public Uri Url { get; }
    public IReadOnlyDictionary<string, string> Headers { get; private init; } = HeaderMerge.Empty;
    public TimeSpan Timeout { get; private init; } = TimeSpan.FromSeconds(10);
    public long? MaxResponseBytes { get; private init; }
    public IpLookupDelegate IpLookup { get; private init; } = DefaultIpLookup;
    public IsCertificateAcceptableDelegate IsCertificateAcceptable { get; private init; } = DefaultIsCertificateAcceptable;

    public SpartanRequest(Uri url)
    {
        Url = url;
    }

    public SpartanRequest(string url)
        : this(new Uri(url))
    {
    }

    /// <summary>Adds a header. Setting the same name twice combines the values with a
    /// comma, per RFC 9110 5.3, rather than replacing the earlier value.</summary>
    public SpartanRequest WithHeader(string name, string value)
        => this with { Headers = HeaderMerge.Add(Headers, name, value) };

    /// <summary>Bounds the entire fetch - DNS resolution, TCP connect, TLS handshake,
    /// and the request/response exchange - so a slow or unresponsive remote server
    /// can't tie up the caller indefinitely. Defaults to ten seconds.</summary>
    public SpartanRequest WithTimeout(TimeSpan timeout)
        => this with { Timeout = timeout };

    /// <summary>Caps how much of the response (status line, headers, and body
    /// combined) is read before the connection is closed early. Unset by default, so
    /// the full response is read.</summary>
    public SpartanRequest WithMaxResponseBytes(long maxBytes)
        => this with { MaxResponseBytes = maxBytes };

    /// <summary>Replaces the DNS resolver used to turn the request's host into
    /// candidate IP addresses. Defaults to the standard DNS lookup.</summary>
    public SpartanRequest WithIpLookupHandler(IpLookupDelegate handler)
        => this with { IpLookup = handler };

    /// <summary>Replaces the TLS certificate check. Defaults to ordinary CA-based
    /// validation - exactly what SslStream would accept with no custom callback.</summary>
    public SpartanRequest WithCertificateValidator(IsCertificateAcceptableDelegate validator)
        => this with { IsCertificateAcceptable = validator };

    /// <summary>Sends the request and returns the completed response.</summary>
    public Task<SpartanResponse> Run(CancellationToken cancellationToken = default)
        => SpartanHttpFetcher.RunAsync(this, cancellationToken);

    private static async Task<IPAddress> DefaultIpLookup(string host, CancellationToken cancellationToken)
    {
        var ip = (await Dns.GetHostAddressesAsync(host).WithCancellation(cancellationToken)).FirstOrDefault() 
            ?? throw new SpartanHttpException(
                "External URL not available.",
                $"No IP address found for host ({host}).");
        return ip;
    }
        
    private static bool DefaultIsCertificateAcceptable(
        Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
        => sslPolicyErrors == SslPolicyErrors.None;
}
