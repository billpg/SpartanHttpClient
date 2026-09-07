using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using billpg.SpartanHttpClient;

namespace billpg.SpartanHttpClientTests;

[TestClass]
public class SpartanHttpFetcherCertificateTests
{
    [TestMethod]
    public async Task DefaultCertificateValidator_RejectsSelfSignedCertificate()
    {
        using var server = new TlsLoopbackServer();
        server.AcceptAndRespond("HTTP/1.1 200 OK\r\n\r\n");

        var request = new SpartanRequest($"https://rutabaga.invalid:{server.Port}/")
            .WithIpLookupHandler((host, ct) => Task.FromResult(new[] { IPAddress.Loopback }))
            .WithTimeout(TimeSpan.FromSeconds(5));

        var ex = await Assert.ThrowsExactlyAsync<SpartanHttpException>(() => request.Run());
        StringAssert.Contains(ex.Message, "TLS handshake");
    }

    [TestMethod]
    public async Task CustomCertificateValidator_AcceptsSelfSignedCertificate_AndReportsHash()
    {
        using var server = new TlsLoopbackServer();
        server.AcceptAndRespond("HTTP/1.1 200 OK\r\n\r\n");

        var expectedHash = Convert.ToBase64String(SHA256.HashData(server.Certificate.RawData));

        var request = new SpartanRequest($"https://rutabaga.invalid:{server.Port}/")
            .WithIpLookupHandler((host, ct) => Task.FromResult(new[] { IPAddress.Loopback }))
            .WithCertificateValidator((url, cert, chain, errors) => true)
            .WithTimeout(TimeSpan.FromSeconds(5));

        var response = await request.Run();

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual(expectedHash, response.RemoteCertificateHash);
    }

    /// <summary>A loopback TLS server presenting a freshly-generated self-signed
    /// certificate for "rutabaga.invalid", mirroring the original HashBack
    /// HttpGetterCertificateTests approach.</summary>
    private sealed class TlsLoopbackServer : IDisposable
    {
        private readonly TcpListener listener;
        public int Port { get; }
        public X509Certificate2 Certificate { get; }

        public TlsLoopbackServer()
        {
            using var rsa = RSA.Create(2048);
            var certRequest = new CertificateRequest(
                "CN=rutabaga.invalid", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            Certificate = certRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void AcceptAndRespond(string rawResponse)
        {
            _ = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var networkStream = client.GetStream();
                using var tls = new SslStream(networkStream, leaveInnerStreamOpen: false);
                try
                {
                    await tls.AuthenticateAsServerAsync(Certificate, clientCertificateRequired: false, checkCertificateRevocation: false);
                }
                catch (Exception)
                {
                    /* The default-validator test deliberately has the client reject the
                     * handshake; the server side of that just sees the stream close. */
                    return;
                }
                var bytes = Encoding.ASCII.GetBytes(rawResponse);
                await tls.WriteAsync(bytes);
            });
        }

        public void Dispose()
        {
            listener.Stop();
            Certificate.Dispose();
        }
    }
}
