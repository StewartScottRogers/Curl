# ADR-0015 — `TransferReport` on `TransferResult` and `ConnectTimings` on `ConnectResult`

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation.

## Context

The Phase 1 HTTP plan (protocol-architect, 2026-09-26, item X2; filed as BL-158) needs
the facts a transfer learns to reach three readers that are not the protocol handler:

- the `-w`/`--write-out` renderer (BL-225), which prints curl 8.21.0's variables
  (<https://curl.se/docs/manpage.html#-w>);
- the redirect follower in `Curl.Core.UnitLibrary` (BL-203), which implements
  `-L`/`--location` around `ProtocolDispatcher` and so must learn the response code,
  the `Location` target and the method from outside the handler;
- the connector, which measures what happens before a handler has a connection, and
  must hand those measurements to the handler that reports them (BL-211).

Today none of this has anywhere to go. Read from the source on 2026-09-26:

- `TransferResult` (`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) is a
  positional record of `ExitCode`, `BytesTransferred`, `ErrorMessage` and
  `SourceLastWriteTimeUtc`, plus the `TimeConditionUnmet` init property. It is built by
  `Success`, `TimeConditionNotMet`, `Failure` and, in places, its constructor.
- `ConnectResult` (`ConnectResult.cs`) is a sealed class with a private constructor,
  built only by `Connected(IConnection)` and `Failed(CurlExitCode, string)`. It carries
  `Connection`, `ExitCode` and `ErrorMessage`.
- `IConnection` (`IConnection.cs`) exposes `RemoteEndPoint` (`EndPoint?`) but no local
  endpoint.
- `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) already reads
  `TimeProvider.GetTimestamp()` to time its dial for the exit 7 message.

`HttpRequestOptions`, the request body, the proxy endpoint and the auth and cookie
seams belong to the same plan and are recorded by ADR-0014; they are not repeated here.

## Decision

Every type below is added to `Curl.Protocol.Abstractions.UnitLibrary`, in the
`Curl.Protocol.Abstractions` namespace, one type per file. BL-160 will add them; none
exists in the code yet.

### Timestamps, not durations or clock reads

Every point in time in `ConnectTimings` and `TransferTimings` is a `long` returned by
`TimeProvider.GetTimestamp()` on the transfer's `ITransferContext.TimeProvider` (or the
`TimeProvider` injected into the connector), taken by the connector or the handler at
the moment the event happens. None is a wall-clock read: no `DateTime.Now`,
`DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `Stopwatch` or `Environment.TickCount`.
Readers turn two timestamps into a duration with
`TimeProvider.GetElapsedTime(start, end)` on the same provider. So every timing is
monotonic, and every timing test runs on a fake time provider with no real clock and
no sleep.

A member whose event did not happen in this transfer is `null`, never `0` and never a
copy of a neighbouring timestamp. How `null` prints (curl prints `0.000000` for
`%{time_appconnect}` on a plain-text transfer) is the renderer's to measure, in BL-225.

### `ConnectTimings`

```csharp
public sealed record ConnectTimings(
    long Started,
    long? NameResolved,
    long Connected,
    long? TlsHandshakeCompleted);
```

| Member | Type | Taken when | Read by |
| --- | --- | --- | --- |
| `Started` | `long` | `ConnectAsync` begins, before name resolution | no variable; it marks where the connect phase began |
| `NameResolved` | `long?` | name resolution finishes; `null` when the host was a literal address and nothing was resolved | `%{time_namelookup}` |
| `Connected` | `long` | the TCP connect to the host or proxy completes; for a tunnel, after the tunnel is open | `%{time_connect}` |
| `TlsHandshakeCompleted` | `long?` | the TLS handshake completes; `null` when `ConnectTarget.UseTls` is `false` | `%{time_appconnect}` |

### `ConnectResult` gains `Timings`, `LocalEndPoint` and `ProxyConnectResponseCode`

```csharp
public ConnectTimings? Timings { get; }                // defaults to null
public System.Net.IPEndPoint? LocalEndPoint { get; }  // defaults to null
public int ProxyConnectResponseCode { get; }           // defaults to 0

public static ConnectResult Connected(IConnection connection);   // unchanged
public static ConnectResult Connected(
    IConnection connection,
    ConnectTimings? timings,
    System.Net.IPEndPoint? localEndPoint = null,
    int proxyConnectResponseCode = 0);
```

- `Connected(IConnection)` keeps its signature and its behaviour, and gives the three
  new members their defaults. Every existing connector and fake compiles unchanged.
- `Failed(CurlExitCode, string)` is unchanged; a failed result's three new members
  are always their defaults. Whether a failed CONNECT's response code must reach
  `%{http_connect}` is for BL-212 to measure; if it must, a later ADR adds it to
  `Failed`.
- `LocalEndPoint` lives on `ConnectResult`, not on `IConnection`, so no implementer of
  `IConnection` changes. It is the local address and port of the socket the connector
  opened: `%{local_ip}` and `%{local_port}`.
- `ProxyConnectResponseCode` is the status code of the proxy's reply to the CONNECT
  that opened a tunnel (ADR-0014, BL-212), and `0` when there was no CONNECT: SOCKS,
  no proxy, or a plain `http://` URL through an HTTP proxy. It is `%{http_connect}`.
- `ConnectTimings` holds points in time only. The local endpoint and the CONNECT code
  are not timings, so they are separate members rather than fields of
  `ConnectTimings`. BL-211's "fill `ConnectTimings` ... and the local and remote
  endpoints" is read as filling these members; the remote endpoint is already
  `IConnection.RemoteEndPoint`.

### `TransferResult.Report`

```csharp
// TransferResult gains, keeping its positional constructor and factories unchanged:
public TransferReport? Report { get; init; }   // defaults to null
```

- `null` means the handler reported nothing beyond the positional members, which is
  what every handler does today. Existing handlers compile and behave unchanged.
- A handler that fills it does so with `with`, or an object initialiser on a factory's
  result: `TransferResult.Success(n) with { Report = report }`.
- A failed transfer may carry a report too: an HTTP 404 under `-f` still has a
  response code, headers and sizes, and curl prints them under `-w`.
- `BytesTransferred` keeps its meaning. When `Report` is present, the renderer takes
  `%{size_download}` and `%{size_upload}` from `Report`, which separates the two
  directions; when `Report` is `null`, it keeps using `BytesTransferred` as it does
  today.

### `TransferReport`

```csharp
public sealed record TransferReport
{
    public int ResponseCode { get; init; }
    public int ProxyConnectResponseCode { get; init; }
    public Version? HttpVersion { get; init; }
    public string? Method { get; init; }
    public IReadOnlyList<KeyValuePair<string, string>> ResponseHeaders { get; init; } = [];
    public string? ContentType { get; init; }
    public string? RedirectUrl { get; init; }
    public string? EffectiveUrl { get; init; }
    public int RedirectCount { get; init; }
    public long HeaderSize { get; init; }
    public long RequestSize { get; init; }
    public long DownloadSize { get; init; }
    public long UploadSize { get; init; }
    public int ConnectionCount { get; init; }
    public System.Net.IPEndPoint? LocalEndPoint { get; init; }
    public System.Net.IPEndPoint? RemoteEndPoint { get; init; }
    public TransferTimings? Timings { get; init; }
}
```

Every member defaults to "not known": `0`, `null` or empty. So a handler sets only what
it learned, and the renderer prints curl's value for an unknown (for example `000` for
`%{response_code}` before any response) by rules BL-225 measures.

| Member | Type | Filled by | `-w` variable(s) | `-L` decision |
| --- | --- | --- | --- | --- |
| `ResponseCode` | `int` | handler, from the last response's status line | `%{response_code}`, `%{http_code}` | Whether to follow at all (a 3xx with a `Location`), and whether POST becomes GET: 301, 302 and 303 rewrite unless the matching `--post30x` is set; 307 and 308 never do (BL-203). |
| `ProxyConnectResponseCode` | `int` | handler, copied from `ConnectResult.ProxyConnectResponseCode` | `%{http_connect}` | — |
| `HttpVersion` | `Version?` | handler, from the last response's status line; `null` for a non-HTTP transfer or before any response | `%{http_version}` (`1.0`, `1.1`, `2`, `3`, or `0` when `null`; formatting measured in BL-225) | — |
| `Method` | `string?` | handler: the method it sent, as sent | `%{method}` | The method the next request starts from before the 301/302/303 rewrite. |
| `ResponseHeaders` | `IReadOnlyList<KeyValuePair<string, string>>` | handler: every header of the last response, name and value as received, in the order received, duplicates kept | `%{header{name}}`, `%{header_json}`, `%{num_headers}` | — (the follower reads `RedirectUrl`, not the raw `Location`) |
| `ContentType` | `string?` | handler: the last response's `Content-Type` value; `null` when absent | `%{content_type}` | — |
| `RedirectUrl` | `string?` | handler: the last response's `Location`, resolved against the request URL, whether or not `-L` was given; `null` when there is none | `%{redirect_url}` | The URL the follower requests next. |
| `EffectiveUrl` | `string?` | follower: the URL of the last request made; `null` from a handler, meaning `ITransferContext.Url` | `%{url_effective}` | Set by the follower after each hop. |
| `RedirectCount` | `int` | follower: redirects followed; `0` from a handler | `%{num_redirects}` | Compared with `--max-redirs` (default 50; exit 47 when exceeded, BL-203). |
| `HeaderSize` | `long` | handler: bytes of every response header block received, status lines and blank lines included | `%{size_header}` | Summed across hops by the follower as BL-203 measures. |
| `RequestSize` | `long` | handler: bytes of every request header block sent, body excluded | `%{size_request}` | Summed across hops as BL-203 measures. |
| `DownloadSize` | `long` | handler: body bytes received, after transfer decoding and before content decoding | `%{size_download}`, `%{speed_download}` | — |
| `UploadSize` | `long` | handler: body bytes sent | `%{size_upload}`, `%{speed_upload}` | — |
| `ConnectionCount` | `int` | handler: `1` for each connection it opened, `0` when it reused one (BL-164) | `%{num_connects}` | Summed across hops by the follower. |
| `LocalEndPoint` | `IPEndPoint?` | handler, copied from `ConnectResult.LocalEndPoint` | `%{local_ip}`, `%{local_port}` | — |
| `RemoteEndPoint` | `IPEndPoint?` | handler, from `IConnection.RemoteEndPoint` when it is an `IPEndPoint`: the proxy's address when a proxy is used, as curl reports it | `%{remote_ip}`, `%{remote_port}` | — |
| `Timings` | `TransferTimings?` | handler; the follower adds `RedirectDuration` | the `%{time_*}` variables below, and the `%{speed_*}` divisors | See `RedirectDuration`. |

- `RedirectUrl` is a `string`, not a `Uri`, because ADR-0010 (Proposed) records URLs
  `System.Uri` cannot round-trip and has not yet decided how to represent them. When
  that decision lands, a later ADR may change this member's type with it.
- `ResponseHeaders` keeps the last response only. curl's `%{header{name}}` reads the
  last response of the last transfer; a follower that needs an earlier hop's headers
  reads that hop's report before merging.

### `TransferTimings`

```csharp
public sealed record TransferTimings(
    long Started,
    ConnectTimings? Connect,
    long? RequestReady,
    long? RequestSent,
    long? FirstByteReceived,
    long Completed)
{
    public TimeSpan RedirectDuration { get; init; }   // defaults to TimeSpan.Zero
}
```

| Member | Type | Taken when | `-w` variable |
| --- | --- | --- | --- |
| `Started` | `long` | the handler's `ExecuteAsync` begins; the origin every other `%{time_*}` is measured from | — |
| `Connect` | `ConnectTimings?` | copied from `ConnectResult.Timings`; `null` when no connection was opened (a reused connection, or a scheme that opens none such as `file://`) | `%{time_namelookup}`, `%{time_connect}`, `%{time_appconnect}` through its members |
| `RequestReady` | `long?` | the connection is ready and the first request byte is about to be sent | `%{time_pretransfer}` |
| `RequestSent` | `long?` | the last request byte, body included, has been sent | `%{time_posttransfer}` |
| `FirstByteReceived` | `long?` | the first response byte is read | `%{time_starttransfer}` |
| `Completed` | `long` | the handler is about to return | `%{time_total}`; with `DownloadSize` and `UploadSize`, `%{speed_download}` and `%{speed_upload}` |
| `RedirectDuration` | `TimeSpan` | set by the follower: the time spent on every hop before the last | `%{time_redirect}` |

- `RedirectDuration` is a duration, not a timestamp, because it is a sum over several
  transfers rather than one event. The follower computes it from the earlier hops'
  `Started` and `Completed` with `TimeProvider.GetElapsedTime`, so it too never reads a
  clock.
- Whether curl measures each `%{time_*}` of a redirected transfer from the first hop's
  start or from the last hop's start is for BL-203 and BL-225 to measure. The raw
  timestamps express either answer.

### What the report does not carry

These `-w` variables come from elsewhere and are not report members:

- `%{exitcode}` and `%{errormsg}`: `TransferResult.ExitCode` and
  `TransferResult.ErrorMessage`, as today.
- `%{url}`, `%{urlnum}`, `%{scheme}`, `%{url.*}`, `%{referer}`, `%{filename_effective}`,
  `%{stdout}`, `%{stderr}`, `%{onerror}`: the command line and the context, known
  before the transfer.
- `%{certs}`, `%{num_certs}`, `%{ssl_verify_result}`, `%{proxy_ssl_verify_result}`,
  `%{tls_earlydata}`, `%{num_retries}`, `%{conn_id}`, `%{xfer_id}`, `%{proxy_used}`,
  `%{time_queue}` and `%{ftp_entry_path}`: no Phase 1 task needs them. Each will be
  added by a later ADR with the task that implements it; adding an init member to
  `TransferReport` breaks no caller.

## Consequences

Good:

- The renderer, the follower and the handler agree on one typed record, and every
  `-w` variable the Phase 1 plan names has exactly one source.
- Every existing handler, connector and test fake compiles and behaves unchanged:
  `Report`, `Timings`, `LocalEndPoint` and `ProxyConnectResponseCode` all default, and
  `Connected(IConnection)` keeps its signature.
- Every timing is testable on a fake `TimeProvider`: tests advance the fake, and the
  asserted durations are exact.
- The follower reads what it needs (`ResponseCode`, `RedirectUrl`, `Method`) without
  parsing response bytes, and so stays in `Curl.Core.UnitLibrary` without referencing
  the HTTP library.

Costs and caveats:

- `TransferReport` is shaped by HTTP; other schemes will fill only some members. FTP's
  `%{response_code}` (the last FTP reply) fits `ResponseCode`, but other
  scheme-specific variables will need members added later.
- `BytesTransferred` and `DownloadSize`/`UploadSize` overlap. The renderer's rule
  (report first, else `BytesTransferred`) must be kept in one place.
- A `long` timestamp is meaningless without the `TimeProvider` that produced it. Every
  reader must use the transfer's own provider; mixing providers gives nonsense
  durations, and the type system does not prevent it.
- The failed-connect path carries no timings or CONNECT code yet; a later ADR adds them
  if BL-212 measures that curl prints them.

## Alternatives considered

- **Durations (`TimeSpan`) instead of timestamps.** Rejected: each `%{time_*}` is a
  duration from the transfer's start, but the connector does not know when the
  transfer started, and a redirect or a reused connection changes the origin. Raw
  timestamps let the reader pick the origin once, and keep the connector and handler
  free of that rule.
- **`DateTimeOffset` from `TimeProvider.GetUtcNow()`.** Rejected: wall-clock time can
  step backwards or jump (clock adjustments), and curl's timings are monotonic.
  `GetTimestamp` is the monotonic source `TimeProvider` offers.
- **Put the report members directly on `TransferResult`.** Rejected: seventeen more
  members on a positional record every handler builds, most meaningless for `file://`,
  `dict://` or `telnet://`. One nullable `Report` adds one member.
- **A dictionary keyed by `-w` variable name.** Rejected, as ADR-0003 and ADR-0006
  rejected dictionaries: it defeats nullable checking, and the follower would read
  strings back into numbers.
- **Add `LocalEndPoint` to `IConnection`.** Rejected: every connection and test fake
  would have to implement it, which breaks "existing handlers compile unchanged" for
  the sake of a value only the connector knows.
- **Put the endpoints and the CONNECT code inside `ConnectTimings`.** Rejected: they
  are not timings, and a name that hides them would mislead the next reader.
- **Let the handler follow redirects and report only the last hop.** Rejected, as ADR-0014's
  `FollowRedirects` row already decided: redirects cross schemes and connections, so following belongs to
  `Curl.Core`, which is why the report carries `RedirectUrl` and `Method` out.
- **`Uri` for `RedirectUrl`.** Rejected for now: ADR-0010 records URLs `Uri` alters or
  refuses, and a `Location` curl follows verbatim must not be rewritten on the way.
