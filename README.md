# SpartanHttpClient

A minimal, one-shot HTTP GET client for dot-net, with a fluent, immutable request
builder.

## 🤔 Why This Exists

`HttpClient` is a general-purpose tool built for connection pooling, keep-alive, and
long-lived reuse. Sometimes what's actually needed is simpler: fetch one URL, once,
with tight control over DNS resolution, which resolved IP addresses are trusted, and
how the TLS certificate is validated - the kind of control a server-side "call this
webhook URL the caller gave us" flow needs to defend against SSRF, without dragging in
`HttpClientFactory`, `SocketsHttpHandler` configuration, or a DI container. This library
speaks HTTP/1.1 directly over `TcpClient`/`SslStream` and exposes those points - IP
lookup (with acceptance filtering folded in) and certificate validation - as plain
delegates.

## 📦 Usage

```csharp
using billpg.SpartanHttpClient;

var response = await new SpartanRequest("https://example.com/")
    .WithHeader("Accept", "text/plain")
    .Run();

Console.WriteLine(response.StatusCode);
Console.WriteLine(response.Body);
```

`SpartanRequest` is immutable - each `With...` call returns a new instance - so a
request can be built up incrementally and reused as a template for several fetches.

### Configuring DNS resolution and IP acceptance

```csharp
var response = await new SpartanRequest(url)
    .WithIpLookupHandler(async (host, ct) => (await Dns.GetHostAddressesAsync(host, ct))[0])
    .Run();
```

`WithIpLookupHandler` takes a delegate that resolves a host to a single address to
connect to - return one, or throw if none are usable. There's no separate acceptance
hook: filtering is the lookup delegate's own job. A handler that needs to weigh several
candidate addresses walks them itself and returns the first it accepts:

```csharp
var response = await new SpartanRequest(url)
    .WithIpLookupHandler(async (host, ct) =>
    {
        foreach (var candidate in await Dns.GetHostAddressesAsync(host))
            if (!IPAddress.IsLoopback(candidate) && !candidate.IsPrivate())
                return candidate;
        throw new SpartanHttpException("URL not acceptable.", $"No acceptable IP address for {host}.");
    })
    .Run();
```

Defaults to the standard DNS resolver's first answer, with no filtering - callers that
need SSRF-style protection (rejecting loopback, link-local, or other private ranges)
supply their own handler that does both the resolving and the filtering.

### Configuring TLS certificate validation

```csharp
var response = await new SpartanRequest(url)
    .WithCertificateValidator((requestUrl, certificate, chain, sslPolicyErrors) =>
        sslPolicyErrors == SslPolicyErrors.None || IsPinnedCertificate(certificate))
    .Run();
```

Defaults to ordinary CA-based validation - exactly what `SslStream` would accept with
no custom callback at all.

### Timeouts and response size

```csharp
var response = await new SpartanRequest(url)
    .WithTimeout(TimeSpan.FromSeconds(5))
    .WithMaxResponseBytes(1_000_000)
    .Run();
```

`WithTimeout` bounds the entire fetch - DNS resolution, TCP connect, TLS handshake, and
the request/response exchange - and defaults to ten seconds. `WithMaxResponseBytes` caps
how much of the response (status line, headers, and body combined) is read before the
connection is closed early; unset by default, so the full response is read.

### Response framing

The body is read according to whatever framing the response headers actually declare -
a `Transfer-Encoding: chunked` reply is decoded, a `Content-Length` reply is read for
exactly that many bytes, and only a response with neither falls back to reading until
the connection closes. This library always sends `Connection: close` and
`Accept-Encoding: identity`, but doesn't assume the server honors either - some servers
keep the connection open regardless, or reply chunked regardless, so relying on the
connection closing to know a response is complete isn't safe on its own.

### The response

`SpartanResponse` carries `StatusCode`, `Headers`, `Body` (as a string), the
`RemoteAddress` actually connected to, and - for `https://` requests -
`RemoteCertificateHash`, the presented certificate's SHA-256 hash, Base64-encoded.

### Errors

A request that can't be completed - a malformed URL, a DNS or TCP connection failure, a
rejected TLS certificate, or a timeout - throws `SpartanHttpException`, with a short
`Title` plus a diagnostic `Message`.

## 🗂️ Project Layout

- `billpg.SpartanHttpClient/` — the library itself. Target framework is
  `netstandard2.0` for broad compatibility.
- `billpg.SpartanHttpClientTests/` — MSTest unit tests. Per project convention, test
  assemblies are named `billpg.(project)Tests`.
- `billpg.SpartanHttpClient/billpg.SpartanHttpClient.slnx` — the solution file,
  referencing both projects.
