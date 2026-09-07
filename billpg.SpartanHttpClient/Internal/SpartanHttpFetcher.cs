using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient.Internal;

/// <summary>
/// Drives the actual GET request over a raw TcpClient/SslStream - deliberately not
/// HttpClient - so DNS resolution, IP acceptance, and certificate validation can all be
/// intercepted via the delegates configured on SpartanRequest.
/// </summary>
internal static class SpartanHttpFetcher
{
    internal static async Task<SpartanResponse> RunAsync(SpartanRequest request, CancellationToken callerCancellationToken)
    {
        ValidateUrlOrThrow(request.Url);

        using var timeoutCts = new CancellationTokenSource(request.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, callerCancellationToken);
        var cancellationToken = linkedCts.Token;

        TcpClient? tcpClient = null;
        Stream? networkStream = null;
        try
        {
            var (connectedTcpClient, connectedStream, remoteAddress, certificateHash) =
                await ConnectAsync(request, cancellationToken).ConfigureAwait(false);
            tcpClient = connectedTcpClient;
            networkStream = connectedStream;

            var hostHeader = request.Url.IsDefaultPort
                ? request.Url.Host
                : $"{request.Url.Host}:{request.Url.Port}";
            var requestLines = new List<string>
            {
                $"GET {request.Url.PathAndQuery} HTTP/1.1",
                $"Host: {hostHeader}",
                "Connection: close",
                "Accept-Encoding: identity",
            };
            requestLines.AddRange(request.Headers.Select(kv => $"{kv.Key}: {kv.Value}"));
            var requestBytes = Encoding.ASCII.GetBytes(string.Join("\r\n", requestLines) + "\r\n\r\n");
            await networkStream.WriteAsync(requestBytes, 0, requestBytes.Length, cancellationToken).ConfigureAwait(false);

            var reader = new ResponseByteReader(networkStream, request.MaxResponseBytes);

            var parseState = ResponseParseState.Initial;
            while (parseState.Stage != ParseStage.Done)
            {
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == null)
                    throw new SpartanHttpException(
                        "External URL not available.",
                        $"Connection to {request.Url} closed before the response headers were complete.");
                parseState = parseState.WithResponseLine(line);
            }

            var body = await ResponseBodyReader.ReadAsync(reader, parseState.Headers, cancellationToken).ConfigureAwait(false);

            return new SpartanResponse
            {
                StatusCode = parseState.StatusCode,
                Headers = parseState.Headers,
                Body = body,
                RemoteAddress = remoteAddress,
                RemoteCertificateHash = certificateHash
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new SpartanHttpException(
                "External URL not available.",
                $"Timed out communicating with {request.Url} after {request.Timeout.TotalSeconds:0.#} seconds.");
        }
        finally
        {
            networkStream?.Dispose();
            tcpClient?.Dispose();
        }
    }

    private static async Task<(TcpClient tcpClient, Stream stream, IPAddress remoteAddress, string? certificateHash)> ConnectAsync(
        SpartanRequest request, CancellationToken cancellationToken)
    {
        var uri = request.Url;
        var remoteAddress = await ResolveAcceptableAddressAsync(request, cancellationToken).ConfigureAwait(false);

        var tcp = new TcpClient();
        try
        {
            await tcp.ConnectAsync(remoteAddress, uri.Port).WithCancellation(cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException)
        {
            tcp.Dispose();
            throw new SpartanHttpException(
                "External URL not available.",
                $"Can't connect to {uri} ({remoteAddress}).");
        }
        catch
        {
            tcp.Dispose();
            throw;
        }
        var networkStream = tcp.GetStream();

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            string? certificateHash = null;
            var tls = new SslStream(networkStream, leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) =>
                {
                    if (certificate == null)
                        return false;

                    X509Certificate2? owned = null;
                    try
                    {
                        var cert2 = certificate as X509Certificate2 ?? (owned = new X509Certificate2(certificate));
                        using var sha256 = SHA256.Create();
                        certificateHash = Convert.ToBase64String(sha256.ComputeHash(cert2.RawData));
                        return request.IsCertificateAcceptable(uri, cert2, chain, sslPolicyErrors);
                    }
                    finally
                    {
                        owned?.Dispose();
                    }
                });
            try
            {
                await tls.AuthenticateAsClientAsync(uri.Host).WithCancellation(cancellationToken).ConfigureAwait(false);
            }
            catch (AuthenticationException ex)
            {
                tls.Dispose();
                tcp.Dispose();
                string certDetail = certificateHash != null
                    ? $" Presented certificate SHA-256 (Base64): {certificateHash}."
                    : "";
                throw new SpartanHttpException(
                    "External URL not available.",
                    $"TLS handshake with {uri} was rejected: {ex.Message}{certDetail}");
            }
            catch
            {
                tls.Dispose();
                tcp.Dispose();
                throw;
            }
            return (tcp, tls, remoteAddress, certificateHash);
        }
        return (tcp, networkStream, remoteAddress, null);
    }

    private static async Task<IPAddress> ResolveAcceptableAddressAsync(SpartanRequest request, CancellationToken cancellationToken)
    {
        var candidates = await request.IpLookup(request.Url.Host, cancellationToken).ConfigureAwait(false);
        foreach (var ip in candidates)
        {
            if (request.IsIpAddressAcceptable(ip))
                return ip;
        }

        throw new SpartanHttpException(
            "External URL not available.",
            $"None of the {candidates.Length} IP address(es) for host ({request.Url.Host}) are acceptable.");
    }

    private static void ValidateUrlOrThrow(Uri url)
    {
        if (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
            throw new SpartanHttpException(
                "URL not acceptable.",
                $"{url.Scheme} URLs are not accepted - only http and https.");
    }
}
