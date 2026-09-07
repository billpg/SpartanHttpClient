using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient;

/// <summary>
/// Resolves a hostname to a set of candidate IP addresses. The default, configured by
/// <see cref="SpartanRequest"/> unless overridden via WithIpLookupHandler, wraps the
/// standard DNS resolver.
/// </summary>
public delegate Task<IPAddress[]> IpLookupDelegate(string host, CancellationToken cancellationToken);

/// <summary>
/// Decides whether a candidate IP address, returned by the configured
/// <see cref="IpLookupDelegate"/>, is acceptable to connect to. Where a lookup returns
/// several candidates, each is offered to this delegate in turn until one is accepted.
/// The default accepts every address unconditionally; supply a handler via
/// WithIpAddressHandler to add restrictions such as rejecting private/loopback ranges.
/// </summary>
public delegate bool IsIpAddressAcceptableDelegate(IPAddress address);

/// <summary>
/// Decides whether a remote TLS certificate is acceptable, given the certificate, its
/// chain, and the policy errors SslStream's own default validation would have raised.
/// The default accepts only when sslPolicyErrors is None - i.e. ordinary CA-based
/// validation, exactly what SslStream would accept with no custom callback at all - so
/// overriding this via WithCertificateValidator is opt-in, for cases such as certificate
/// pinning or accepting a self-signed certificate.
/// </summary>
public delegate bool IsCertificateAcceptableDelegate(
    Uri url, X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors);
