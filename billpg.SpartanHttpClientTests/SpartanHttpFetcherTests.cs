using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using billpg.SpartanHttpClient;

namespace billpg.SpartanHttpClientTests;

[TestClass]
public class SpartanHttpFetcherTests
{
    /// <summary>Points a request at a loopback TcpListener without touching real DNS,
    /// via a custom IP lookup handler - the same seam the constructor-injected
    /// dnsLookup delegate provided in the original HashBack HttpGetter.</summary>
    private static SpartanRequest RequestForPort(int port, string scheme = "http")
        => new SpartanRequest($"{scheme}://rutabaga.invalid:{port}/")
            .WithIpLookupHandler((host, ct) => Task.FromResult(IPAddress.Loopback));

    [TestMethod]
    public async Task Run_ReturnsStatusCodeHeadersAndBody()
    {
        using var server = new LoopbackServer();
        server.RespondWith(
            "HTTP/1.1 200 OK\r\n" +
            "X-Farm: Rutabaga Farms Inc\r\n" +
            "\r\n" +
            "Grown fresh daily.");

        var response = await RequestForPort(server.Port).Run();

        Assert.AreEqual(200, response.StatusCode);
        Assert.AreEqual("Rutabaga Farms Inc", response.Headers["X-Farm"]);
        Assert.AreEqual("Grown fresh daily.", response.Body);
        Assert.AreEqual(IPAddress.Loopback, response.RemoteAddress);
        Assert.IsNull(response.RemoteCertificateHash);
    }

    [TestMethod]
    public async Task Run_SendsConfiguredHeaders()
    {
        using var server = new LoopbackServer();
        server.RespondWith("HTTP/1.1 200 OK\r\n\r\n");

        await RequestForPort(server.Port)
            .WithHeader("X-Vegetable", "Rutabaga")
            .Run();

        var sentRequest = server.ReceivedRequestText;
        StringAssert.Contains(sentRequest, "X-Vegetable: Rutabaga");
        StringAssert.Contains(sentRequest, "GET / HTTP/1.1");
        StringAssert.Contains(sentRequest, $"Host: rutabaga.invalid:{server.Port}");
    }

    [TestMethod]
    public async Task Timeout_ThrowsPromptly_WhenServerNeverResponds()
    {
        using var server = new LoopbackServer();
        server.AcceptButNeverRespond();

        var request = RequestForPort(server.Port).WithTimeout(TimeSpan.FromMilliseconds(200));

        var stopwatch = Stopwatch.StartNew();
        var ex = await Assert.ThrowsExactlyAsync<SpartanHttpException>(() => request.Run());
        stopwatch.Stop();

        StringAssert.Contains(ex.Message, "Timed out");
        Assert.IsLessThan(2000, stopwatch.ElapsedMilliseconds, "Timeout should fire close to the configured 200ms, not fall back to a much larger default.");
    }

    [TestMethod]
    public async Task UnsupportedScheme_ThrowsWithoutConnecting()
    {
        var request = new SpartanRequest("ftp://rutabaga.invalid/");
        var ex = await Assert.ThrowsExactlyAsync<SpartanHttpException>(() => request.Run());
        StringAssert.Contains(ex.Message, "ftp");
    }

    [TestMethod]
    public async Task ContentLength_StopsReadingWithoutWaitingForConnectionClose()
    {
        using var server = new LoopbackServer();
        server.RespondThenKeepOpen(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Length: 11\r\n" +
            "\r\n" +
            "hello world",
            TimeSpan.FromSeconds(3));

        var stopwatch = Stopwatch.StartNew();
        var response = await RequestForPort(server.Port).WithTimeout(TimeSpan.FromSeconds(2)).Run();
        stopwatch.Stop();

        Assert.AreEqual("hello world", response.Body);
        Assert.IsLessThan(1000, stopwatch.ElapsedMilliseconds,
            "A declared Content-Length should end the read as soon as that many bytes arrive, not wait for the server to close the connection.");
    }

    [TestMethod]
    public async Task ChunkedTransferEncoding_IsDecoded()
    {
        using var server = new LoopbackServer();
        server.RespondThenKeepOpen(
            "HTTP/1.1 200 OK\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            "5\r\nhello\r\n" +
            "6\r\n world\r\n" +
            "0\r\n\r\n",
            TimeSpan.FromSeconds(3));

        var stopwatch = Stopwatch.StartNew();
        var response = await RequestForPort(server.Port).WithTimeout(TimeSpan.FromSeconds(2)).Run();
        stopwatch.Stop();

        Assert.AreEqual("hello world", response.Body);
        Assert.IsLessThan(1000, stopwatch.ElapsedMilliseconds,
            "The terminating zero-length chunk should end the read, not the connection closing - a server can send chunked replies even though this client only asked for identity encoding.");
    }

    [TestMethod]
    public async Task ChunkedTransferEncoding_TrailerHeadersAreConsumedAndIgnored()
    {
        using var server = new LoopbackServer();
        server.RespondWith(
            "HTTP/1.1 200 OK\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            "5\r\nhello\r\n" +
            "0\r\n" +
            "X-Trailer: ignored\r\n" +
            "\r\n");

        var response = await RequestForPort(server.Port).Run();
        Assert.AreEqual("hello", response.Body);
    }

    [TestMethod]
    public async Task ChunkedTransferEncoding_MalformedChunkSize_Throws()
    {
        using var server = new LoopbackServer();
        server.RespondWith(
            "HTTP/1.1 200 OK\r\n" +
            "Transfer-Encoding: chunked\r\n" +
            "\r\n" +
            "not-hex\r\n");

        var ex = await Assert.ThrowsExactlyAsync<SpartanHttpException>(() => RequestForPort(server.Port).Run());
        StringAssert.Contains(ex.Message, "chunk size");
    }

    [TestMethod]
    public async Task MaxResponseBytes_CapsHowMuchIsRead()
    {
        using var server = new LoopbackServer();
        var body = new string('r', 5000);
        server.RespondWith("HTTP/1.1 200 OK\r\n\r\n" + body);

        var response = await RequestForPort(server.Port)
            .WithMaxResponseBytes(100)
            .Run();

        int totalLength = "HTTP/1.1 200 OK\r\n\r\n".Length + response.Body.Length;
        Assert.IsLessThanOrEqualTo(100, totalLength, $"Expected the capped read to stay near 100 bytes total, got {totalLength}.");
    }

    /// <summary>A minimal raw-socket loopback server for driving SpartanHttpFetcher's
    /// TCP path without any real network dependency, mirroring the approach the
    /// original HashBack HttpGetter tests used.</summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly TcpListener listener;
        public int Port { get; }
        public string ReceivedRequestText { get; private set; } = "";

        public LoopbackServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void RespondWith(string rawResponse)
        {
            _ = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                ReceivedRequestText = await ReadRequestAsync(stream);
                var bytes = Encoding.ASCII.GetBytes(rawResponse);
                await stream.WriteAsync(bytes);
            });
        }

        /// <summary>Writes the response but keeps the socket open afterward, simulating
        /// a server that ignores this client's "Connection: close" - proves the client
        /// isn't relying on the connection closing to know the response is complete.</summary>
        public void RespondThenKeepOpen(string rawResponse, TimeSpan keepOpenFor)
        {
            _ = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                ReceivedRequestText = await ReadRequestAsync(stream);
                var bytes = Encoding.ASCII.GetBytes(rawResponse);
                await stream.WriteAsync(bytes);
                await Task.Delay(keepOpenFor).ConfigureAwait(false);
            });
        }

        public void AcceptButNeverRespond()
        {
            _ = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
            });
        }

        private static async Task<string> ReadRequestAsync(NetworkStream stream)
        {
            var buffer = new byte[4096];
            var text = new StringBuilder();
            while (!text.ToString().Contains("\r\n\r\n"))
            {
                int bytesIn = await stream.ReadAsync(buffer);
                if (bytesIn <= 0)
                    break;
                text.Append(Encoding.ASCII.GetString(buffer, 0, bytesIn));
            }
            return text.ToString();
        }

        public void Dispose() => listener.Stop();
    }
}
