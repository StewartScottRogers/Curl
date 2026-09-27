# ADR-0046 — The transfer context carries a transfer event sink for `-v` and `--trace`

- **Status:** Accepted
- **Date:** 2026-09-26
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

`-v`, `--trace` and `--trace-ascii` show what happened inside a transfer: the connection,
the TLS handshake, the header bytes sent and received, the body bytes, and curl's own
`* ` info lines. A handler today reports nothing until it returns its `TransferResult`, and
the connector reports nothing but its `ConnectResult`, so `Curl.Output` has nothing to
render (BL-228 formats `-v`, BL-229 formats the trace dumps, BL-242 wires them in
`Curl.Console`).

curl 8.21.0 (x86_64-w64-mingw32, Schannel) was measured on 2026-09-26 against a local
`python -m http.server` serving a 6-byte `f.txt`:

- `curl -s -v http://127.0.0.1:18163/f.txt -o out 2>v.txt` wrote, with CRLF line ends
  on the info lines and CR CR LF on the header lines (the header's own CRLF, then curl's
  line end):

  ```
  *   Trying 127.0.0.1:18163...
  * Established connection to 127.0.0.1 (127.0.0.1 port 18163) from 127.0.0.1 port 51270 
  * using HTTP/1.x
  > GET /f.txt HTTP/1.1
  > Host: 127.0.0.1:18163
  > User-Agent: curl/8.21.0
  > Accept: */*
  > 
  * Request completely sent off
  * HTTP 1.0, assume close after body
  < HTTP/1.0 200 OK
  < Server: SimpleHTTP/0.6 Python/3.11.15
  < ...
  < 
  { [6 bytes data]
  * shutting down connection #0
  ```

- `--trace tr.txt` for the same request wrote the whole request head as **one**
  `=> Send header, 84 bytes (0x54)` block, each response header line (and the final blank
  line) as its **own** `<= Recv header, N bytes` block, the body as
  `<= Recv data, 6 bytes (0x6)`, and the info lines as `* ` lines between the blocks.
  `--trace-ascii` has the same blocks and boundaries. With `-d hi`, `-v` showed
  `} [2 bytes data]` for the body sent.
- `curl -v https://example.com/` added the TLS lines the Schannel build prints:
  `* schannel: disabled automatic use of client certificate`, `* ALPN: curl offers http/1.1`,
  `* ALPN: server accepted http/1.1`, and after the request the three `* schannel:`
  renegotiation lines. It printed no protocol version, cipher or certificate lines; the
  OpenSSL build on Linux and macOS prints those (`* SSL connection using TLSv1.3 / ...`,
  `* Server certificate:` and its fields).
- A refused connection printed `* connect to 127.0.0.1 port 1 from 0.0.0.0 port 51273 failed:
  Connection refused` and `* Failed to connect to 127.0.0.1:1 after 2028 ms: ...`, from inside
  the connect.

So the events are libcurl's debug-callback kinds (text, header in and out, data in and out,
TLS data), the event boundaries are what the trace dumps show, and some events happen
inside the connector before any handler code runs again.

ADR-0045 (BL-133) gave `ITransferContext` a progress sink, `ITransferProgress`, for the
progress meter. This ADR decides whether the event sink extends that sink or sits beside it.

## Decision

### A sibling sink, not an extension of `ITransferProgress`

`Curl.Protocol.Abstractions.UnitLibrary` gains a new interface, `ITransferEvents`, beside
`ITransferProgress`, and `ITransferContext` gains a second member for it:

- `ITransferEvents Events { get; }` - where the handler and its connector report transfer
  events. Never `null`.

`TransferContext` implements it as an `init` property defaulting to
`NoTransferEvents.Instance`, a sealed class whose members do nothing, the pattern
ADR-0045 used for `Progress`. As there, the interface member has no default
implementation.

**The default sink does nothing**, so every handler, connector and test that ignores it is
unaffected: it keeps compiling and behaves exactly as before, and no output appears unless
`Curl.Console` passes a real sink for `-v` or `--trace`.

A handler that receives body bytes reports them to both sinks: the running total to
`Progress` and the bytes themselves to `Events`. The two are independent; neither calls the
other.

### The event kinds and their payloads

Every member is synchronous and returns `void`, like `ITransferProgress`. Byte payloads are
`ReadOnlySpan<byte>`, valid only for the duration of the call; a sink that keeps them
copies them. No member takes or reads a timestamp: `--trace-time` is stamped by the
consumer from the injected `TimeProvider` when the event arrives (ADR-0045's rule).

| Member | curl kind | Payload | Rendered by `-v` as |
| --- | --- | --- | --- |
| `ReportInfo(string text)` | `CURLINFO_TEXT` | One info line, without the `* ` prefix and without a line end. The reporter writes curl's text for it (measured per task). | `* ` + text |
| `ReportConnectionOpened(ConnectionOpenedEvent opened)` | text | `HostName` (the name connected to, as in the URL or proxy), `RemoteEndPoint` and `LocalEndPoint` (`IPEndPoint`), `ConnectionNumber` (`long`, curl's `#N`). | `* Established connection to <host> (<ip> port <p>) from <ip> port <p> ` |
| `ReportConnectionReused(ConnectionReusedEvent reused)` | text | `HostName`, `Port`, `ConnectionNumber`. | `* Re-using existing ... connection with host <host>` |
| `ReportTlsHandshake(TlsHandshakeEvent handshake)` | text | `ProtocolVersion` (`SslProtocols`), `CipherSuite` (`TlsCipherSuite?`), `NegotiatedApplicationProtocol` (`string?`, the ALPN result), `OfferedApplicationProtocols` (`IReadOnlyList<string>`), `ServerCertificate` (`X509Certificate2?`) and `CertificateVerified` (`bool`). | The platform build's lines: ALPN only on Schannel, version, cipher and certificate fields on OpenSSL (ADR-0009). |
| `ReportTlsData(ReadOnlySpan<byte> bytes, bool sent)` | `CURLINFO_SSL_DATA_IN/OUT` | Raw TLS record bytes. Reserved: `SslStream` does not expose them, so no BCL provider reports them, and neither does curl's Schannel build in `--trace`. | nothing (as curl) |
| `ReportRequestHeader(ReadOnlySpan<byte> bytes)` | `CURLINFO_HEADER_OUT` | The request head bytes exactly as written, CRLFs included, one call per write of a head (measured: one 84-byte block for the whole head). | `> ` per line |
| `ReportResponseHeader(ReadOnlySpan<byte> bytes)` | `CURLINFO_HEADER_IN` | One received header line, its CRLF included, one call per line, the status line and the final blank line each one call (measured). | `< ` per line |
| `ReportDataSent(ReadOnlySpan<byte> bytes)` | `CURLINFO_DATA_OUT` | Body bytes as written to the connection, one call per write. | `} [N bytes data]` |
| `ReportDataReceived(ReadOnlySpan<byte> bytes)` | `CURLINFO_DATA_IN` | Body bytes as delivered, one call per read. | `{ [N bytes data]` |

The structured events (`ConnectionOpened`, `ConnectionReused`, `TlsHandshake`) carry facts,
not text: `Curl.Output`'s verbose formatter (BL-228) owns curl's wording for them, per
platform build, and renders them as `* ` lines; the trace formatter (BL-229) renders the
same lines as `* ` text in the dump. Everything curl prints that is not one of those facts
(`Trying`, `using HTTP/1.x`, `Request completely sent off`, `shutting down connection #0`,
a connect failure) is `ReportInfo` text written by whichever component knows it, measured
by the task that adds it.

The event record types are sealed records in `Curl.Protocol.Abstractions` with required
members; they are added with the interface.

### The connector reports through `ConnectTarget`

Connection and TLS events happen inside `IConnector.ConnectAsync`, before the handler runs
again, and a connect failure is reported there. `ConnectTarget` therefore gains
`ITransferEvents Events { get; init; }`, defaulting to `NoTransferEvents.Instance`; a
handler sets it from `context.Events`. The connector reports `Trying` and failure text,
`ReportConnectionOpened` and `ReportTlsHandshake`, in the order they happen. No connector
learns about `ITransferContext`.

### Who does what next

- Adding `ITransferEvents`, `NoTransferEvents`, the event records, `ITransferContext.Events`,
  `TransferContext.Events` and `ConnectTarget.Events` is a follow-up task on
  `Curl.Protocol.Abstractions.UnitLibrary`, the way BL-134 follows ADR-0045.
- Carrying `Events` across redirect hops in `RedirectFollower.NextHop` must be done in the
  same change as BL-310's `Progress` copy or beside it: the default hides a missing copy
  from the compiler.
- Reporting from `TcpConnector`, `SslStreamTlsProvider` and the HTTP handler, and the
  formatters (BL-228, BL-229) and console wiring (BL-242), are their own tasks. Each
  measures the text it writes.

## Consequences

Good:

- Every existing handler, connector and test is unchanged: they get the do-nothing sink.
- Event boundaries are the trace dump's block boundaries, so BL-229 can be byte-equal to
  curl without re-splitting bytes, and a test pins events in order with a recording sink,
  no socket and no clock.
- Spans cost no allocation in the copy loops when nobody is listening.
- `Curl.Output` owns all of curl's wording for the structured facts, so the Schannel and
  OpenSSL differences live in one formatter.

Costs and caveats:

- `ITransferContext` and `ConnectTarget` each grow by a member, and a hand copy
  (`RedirectFollower.NextHop`) drops it silently unless it copies it.
- A handler that reads body bytes calls two sinks per read.
- `ReportInfo` text is still curl's wording written in the reporter, so it is spread across
  the libraries that report it; each task measures its own lines.
- `ReportTlsData` exists and nothing calls it until a provider can see TLS records.

## Alternatives considered

- **Extend `ITransferProgress` with the event members.** Rejected: the meter and the verbose
  and trace output are different consumers, switched on by different options, and every
  progress sink would have to implement nine members it ignores. Counts and bytes are also
  different contracts: a running total is idempotent, a byte event is not.
- **One method, `Report(TransferEventKind kind, ReadOnlySpan<byte> bytes)`, like libcurl's
  debug callback.** Rejected: connection and TLS facts would have to be pre-rendered to text
  by the connector, putting curl's per-platform wording in `Curl.Networking` and making the
  facts untestable except as strings.
- **Render every event as text at the source.** Rejected for the same reason; the facts
  would also be unusable by a future `-w` or `--trace-config` consumer.
- **Report connection and TLS events from the handler after `ConnectAsync` returns, from
  `ConnectResult`.** Rejected: `Trying` and the connect-failure lines come before or instead
  of a result, and curl prints them as they happen.
- **An `IObservable<T>` or `IProgress<T>` from the BCL.** Rejected: both allocate an event
  object per report, and `Progress<T>` posts to a synchronization context, which a copy loop
  must not pay for (ADR-0045).
- **A `null` sink meaning "nobody is listening".** Rejected, as in ADR-0045: a missed null
  check is a `NullReferenceException` in a copy loop.
