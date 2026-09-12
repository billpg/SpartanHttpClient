using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient;

/// <summary>
/// Resolves a hostname to the single IP address to connect to - return one, or throw if
/// none are usable. Where a lookup could return several candidates, weighing them (and
/// rejecting any that shouldn't be trusted, such as private or loopback ranges) is this
/// delegate's own job. The default, configured by <see cref="SpartanRequest"/> unless
/// overridden via WithIpLookupHandler, wraps the standard DNS resolver and returns its
/// first answer, with no filtering.
/// </summary>
public delegate Task<IPAddress> IpLookupDelegate(string host, CancellationToken cancellationToken);

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

public delegate Task<SpartanResponse> SpartanRequestRunner(SpartanRequest request, CancellationToken cancellationToken);
