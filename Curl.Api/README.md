# Curl.Api

The .NET programming interface to Curl (ADR-0506): an async client over curl's engine, with
`HttpClient`-shaped ergonomics where they fit and curl's own options where they do not. It is
BCL only and native-AOT safe, and every member that waits takes a `CancellationToken`.

Every code block below is also a test in `Curl.Api.UnitTests/Readme` (run against a fake engine, so
nothing touches the network), and a second test fails if a block here differs from the tested
source or if a public type of `Curl.Api` is not named in this file. The samples assume these
`using` directives, and the later ones assume the `client` made in the first, a `cancellationToken`,
and a `destination` stream:

<!-- sample: usings -->
```csharp
using System.Net;
using Curl.Api;
using Curl.Api.Errors;
using Curl.Api.Options.Http;
```

## Create a client

`CurlClient` is the entry point. `CurlClient.Create()` builds one over a new managed engine that
the client owns and disposes; `CurlClientOptions` holds the defaults applied to every transfer
(`UserAgent`, `Timeout`, `ConnectTimeout`, plus the option groups below, `MaxConnections` and
`MaxConnectionsPerHost` for concurrent transfers, and `Stores`). A value left unset is libcurl's
own default. Make one client and share it: it is built for concurrent use.

<!-- sample: create -->
```csharp
await using CurlClient client = CurlClient.Create(new CurlClientOptions
{
    UserAgent = "my-app/1.0",
    Timeout = TimeSpan.FromSeconds(30),
    ConnectTimeout = TimeSpan.FromSeconds(5),
});
```

## Send a request and stream the body

`CurlRequest` (method, URL, headers, optional body) is an immutable record; change one part with
`with`. `CurlClient.SendAsync` returns a `CurlResponse` once the body starts or the transfer ends,
so the headers are there while the body is still arriving. `CurlResponse.Body` is a stream you
read asynchronously (`ReadAsync`, `CopyToAsync`); dispose it when you are done, which aborts a
transfer that has not finished.

<!-- sample: send -->
```csharp
CurlRequest request = new(HttpMethod.Get, "https://example.com/data.bin");
CurlResponse response = await client.SendAsync(request, cancellationToken);

Console.WriteLine($"{response.StatusCode} {response.ReasonPhrase} over HTTP {response.Version}");
foreach (KeyValuePair<string, string> header in response.Headers)
{
    Console.WriteLine($"{header.Key}: {header.Value}");
}

await using Stream body = response.Body;
await body.CopyToAsync(destination, cancellationToken);
```

## Set options

A request carries its headers, a body and optional option groups that override the client's: the
transport groups `CurlNetworkOptions`, `CurlTlsOptions` (with `CurlTlsPeerOptions`) and
`CurlProxyOptions`, and the protocol groups `CurlHttpOptions`, `CurlAuthenticationOptions`,
`CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions`, `CurlTftpOptions`, `CurlTelnetOptions` and
`CurlRtspOptions`. A group set on a request replaces the client's whole group. Each value is
checked as it is set.

<!-- sample: options -->
```csharp
CurlRequest request = new(HttpMethod.Get, "https://example.com/redirected")
{
    Headers = [new("Accept", "application/json"), new("X-Trace", "42")],
    Http = new CurlHttpOptions { FollowLocation = FollowLocation.All, MaxRedirects = 5 },
    Network = new CurlNetworkOptions { TcpKeepAlive = true },
};
CurlResponse response = await client.SendAsync(request, cancellationToken);
await response.Body.DisposeAsync();
```

## Upload a body

`CurlContent` is the type of `CurlRequest.Body`. A `byte[]` converts to it implicitly
(`CurlContent.FromBytes`), `CurlContent.FromStream` uploads a stream (give its length when you
know it), and `CurlContent.Empty` is an empty body.

<!-- sample: upload -->
```csharp
CurlRequest json = new(HttpMethod.Post, "https://example.com/items")
{
    Headers = [new("Content-Type", "application/json")],
    Body = "{\"name\":\"widget\"}"u8.ToArray(),
};
CurlRequest large = new(HttpMethod.Put, "https://example.com/upload")
{
    Body = CurlContent.FromStream(file, length: file.Length),
};
await (await client.SendAsync(json, cancellationToken)).Body.DisposeAsync();
await (await client.SendAsync(large, cancellationToken)).Body.DisposeAsync();
```

(This sample also assumes a `file` stream.)

## Upload multipart

`CurlMultipartContent` is a `CurlContent` made of immutable `CurlMimePart`s: text, bytes, a file
path, a stream or a nested multipart, each with an optional name, file name, content type, encoder
and headers.

<!-- sample: multipart -->
```csharp
CurlMultipartContent form = new(
    CurlMimePart.Text("title", "Holiday"),
    CurlMimePart.Bytes("photo", new byte[] { 0xFF, 0xD8, 0xFF }) with
    {
        FileName = "photo.jpg",
        ContentType = "image/jpeg",
    });
CurlRequest request = new(HttpMethod.Post, "https://example.com/albums") { Body = form };
CurlResponse response = await client.SendAsync(request, cancellationToken);
await response.Body.DisposeAsync();
```

## Cancel and handle errors

Cancelling the token aborts the transfer (also while its body is being read) and throws
`OperationCanceledException`. Every other failure, a timeout included, is a `CurlException` with the
`CurlCode` in `Code` and curl's error text as the message; `CurlCodeText.Of` gives curl's
`curl_easy_strerror` text for a code. Disposing the client cancels every transfer still running.

<!-- sample: cancel -->
```csharp
using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(TimeSpan.FromSeconds(10));
try
{
    CurlResponse response = await client.SendAsync(new CurlRequest(HttpMethod.Get, "https://example.com/slow"), timeout.Token);
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null, timeout.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled: the transfer was aborted.");
}
catch (CurlException failure)
{
    Console.WriteLine($"curl error {(int)failure.Code}: {failure.Message}");
}
```

## Report progress

Set `CurlRequest.Progress` to an `IProgress<CurlTransferProgress>`. Each report carries the
download and upload totals and counts so far, the speeds and a `CurlTransferPhase`
(`Connecting`, `Uploading` or `Downloading`), and arrives on the engine's thread, so `Progress<T>`
is the usual way to hop to your own context. A sink that throws aborts the transfer with a
`CurlException`.

<!-- sample: progress -->
```csharp
Progress<CurlTransferProgress> progress = new(report =>
    Console.WriteLine($"{report.Phase}: {report.DownloadNow} of {report.DownloadTotal} bytes"));
CurlRequest request = new(HttpMethod.Get, "https://example.com/big.iso") { Progress = progress };
CurlResponse response = await client.SendAsync(request, cancellationToken);
await response.Body.CopyToAsync(Stream.Null, cancellationToken);
```

## Use a WebSocket

`CurlClient.ConnectWebSocketAsync` connects (`CurlWebSocketOptions` turns off the automatic pong
or selects raw mode) and returns a `CurlWebSocket`. `ReceiveAsync` gives one whole
`CurlWebSocketMessage` (fragments joined; its `CurlWebSocketMessageType` is `Text`, `Binary`,
`Close`, `Ping` or `Pong`), `ReadMessagesAsync` yields messages through the peer's close, and
`SendTextAsync`, `SendAsync`, `PingAsync`, `PongAsync` and `CloseAsync` send. `ReceiveFrameAsync`
(giving a `CurlWebSocketFramePiece`), `SendFrameAsync`, `SendRawAsync` and `CurrentFrame` are the
frame-level API.

<!-- sample: websocket -->
```csharp
await using CurlWebSocket socket = await client.ConnectWebSocketAsync("wss://example.com/chat", cancellationToken);
await socket.SendTextAsync("hello", cancellationToken);
await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync(cancellationToken))
{
    if (message.Type == CurlWebSocketMessageType.Text)
    {
        Console.WriteLine(message.Text);
    }
}

await socket.CloseAsync(cancellationToken: cancellationToken);
```

## Run concurrent transfers

Every `SendAsync` of a client runs on one shared multi handle, so starting several and awaiting
them together overlaps them; `CurlClientOptions.MaxConnections` and `MaxConnectionsPerHost` limit
the connections.

<!-- sample: concurrent -->
```csharp
string[] urls = ["https://example.com/a", "https://example.com/b", "https://example.com/c"];
Task<CurlResponse>[] transfers = [.. urls.Select(url => client.SendAsync(new CurlRequest(HttpMethod.Get, url), cancellationToken))];
foreach (CurlResponse response in await Task.WhenAll(transfers))
{
    await using Stream body = response.Body;
    await body.CopyToAsync(Stream.Null, cancellationToken);
}
```

## Use it with HttpClient

`CurlHttpMessageHandler` is an `HttpMessageHandler` over a `CurlClient` that it does not own, so
existing `HttpClient` code runs on curl's engine. It has the `SocketsHttpHandler` properties
`AutomaticDecompression`, `AllowAutoRedirect` (default `true`), `MaxAutomaticRedirections`,
`Credentials`, `PreAuthenticate`, `UseCookies`, `CookieContainer`, `Proxy`, `UseProxy` and
`DefaultProxyCredentials`. HTTP versions 1.0 to 3.0 are accepted, and `VersionPolicy` is honoured.

<!-- sample: httpclient -->
```csharp
using CurlHttpMessageHandler handler = new(client)
{
    AutomaticDecompression = DecompressionMethods.All,
    MaxAutomaticRedirections = 10,
};
using HttpClient http = new(handler);
string text = await http.GetStringAsync("https://example.com/", cancellationToken);
```

## Cookies, HSTS and Alt-Svc

`CurlStores` holds a `CurlCookieStore` (Netscape cookie-file lines), a `CurlHstsStore` and a
`CurlAltSvcStore` (with `CurlAltSvcEntry` lines), each loadable from and savable to curl's file
format. Give clients one `CurlStores` through `CurlClientOptions.Stores` to share it; otherwise
`CurlClient.Stores`, `Cookies`, `Hsts` and `AltSvc` make the client's own on first use.

## What differs from HttpClient

- `SendAsync` does not follow redirects, decompress or keep cookies unless you set the options
  (`CurlHttpOptions`, the stores); `CurlHttpMessageHandler` gives `HttpClient`'s defaults instead.
- Failures are `CurlException` (with curl's code), not `HttpRequestException`; the handler
  converts them.
- A non-2xx status is a normal `CurlResponse`; nothing throws for it.
- `Headers` is an ordered list keeping the casing and duplicates received, not a header table.
- The body stream is async-only: a synchronous `Read` throws `NotSupportedException`.
- There is no `HttpClient.Timeout`; use `CurlClientOptions.Timeout` and `ConnectTimeout`.

## Public types

| Type | What it does |
| --- | --- |
| `CurlClient` | Sends requests, opens WebSockets, owns the stores and the shared multi handle. |
| `CurlClientOptions` | The defaults for every transfer of a client. |
| `CurlRequest` | One request: method, URL, headers, body, option groups and progress sink. |
| `CurlResponse` | Status, version, headers, trailers, transfer info and the streamed body. |
| `CurlContent` | A request body: bytes, a stream or multipart. |
| `CurlMultipartContent` | A multipart body. |
| `CurlMimePart` | One part of a multipart body. |
| `CurlTransferProgress` | One progress report. |
| `CurlTransferPhase` | Connecting, uploading or downloading. |
| `CurlException` | The one exception for a failed transfer, with its `CurlCode`. |
| `CurlCodeText` | curl's error text for a `CurlCode`. |
| `CurlWebSocket` | An open WebSocket connection. |
| `CurlWebSocketOptions` | Auto-pong and raw-mode switches for a WebSocket. |
| `CurlWebSocketMessage` | One whole WebSocket message. |
| `CurlWebSocketMessageType` | Text, binary, close, ping or pong. |
| `CurlWebSocketFramePiece` | A piece of a frame from `ReceiveFrameAsync`. |
| `CurlHttpMessageHandler` | An `HttpMessageHandler` over a `CurlClient`. |
| `CurlStores` | The cookie, HSTS and Alt-Svc stores of a client. |
| `CurlCookieStore` | Cookies as Netscape cookie-file lines. |
| `CurlHstsStore` | HSTS entries. |
| `CurlAltSvcStore` | The alt-svc cache. |
| `CurlAltSvcEntry` | One alt-svc cache line. |
| `CurlNetworkOptions`, `CurlTlsOptions`, `CurlTlsPeerOptions`, `CurlProxyOptions` | The transport option groups. |
| `CurlHttpOptions`, `CurlAuthenticationOptions`, `CurlFtpOptions`, `CurlSshOptions`, `CurlMailOptions`, `CurlTftpOptions`, `CurlTelnetOptions`, `CurlRtspOptions` | The protocol option groups. |

## Folder map

Every folder of `Curl.Api` is one concern; the namespace is `Curl.Api` (`Curl.Api.Errors` for
`Errors/`).

| Folder | Holds |
| --- | --- |
| `Client/`, `Composition/` | `CurlClient` and `CurlClientOptions`; the half of `CurlClient` that builds its engine (`Create`). |
| `Messages/` | `CurlRequest`, `CurlResponse` and the `SendAsync` half of `CurlClient`. |
| `RequestBodies/`, `Mime/` | `CurlContent` and its bytes and stream bodies; `CurlMultipartContent` and `CurlMimePart`. |
| `ResponseBodies/` | The streamed, async-only `CurlResponse.Body`. |
| `Options/` | The transport and protocol option groups and their guards. |
| `Cancellation/`, `Progress/` | Cancelling a transfer by token; `CurlTransferProgress` reporting. |
| `Concurrency/` | The shared multi handle behind every `SendAsync`, and the connection limits. |
| `Stores/` | `CurlStores` and the cookie, HSTS and Alt-Svc stores. |
| `WebSockets/` | `CurlWebSocket` and its messages. |
| `HttpMessageHandler/` | `CurlHttpMessageHandler` (`Core/`, `Versions/`, `Proxy/`, `Behaviour/`). |
| `Errors/` | `CurlException` and `CurlCodeText`. |

## Quality gates and sibling projects

`Curl.Api` is held to the solution's gates: 100% line and branch coverage from the fast tests,
cyclomatic complexity at most 10 per method and a CRAP score at most 30. Its tests are
`Curl.Api.UnitTests`, which also test `Curl.Api.UnitLibrary`: run
`dotnet test Curl.Api.UnitTests --filter "TestCategory!=Integration"`. `Curl.Api` references
`Curl.Api.UnitLibrary` (the engine contracts, callbacks, codes, info and option records that carry
no I/O) and `Curl.Engine.UnitLibrary` (the engine it sends through); `Curl.Api` is not itself a
`Curl.<Area>.UnitLibrary`, but a naming exception like `Curl.Console` and `Libcurl`.
