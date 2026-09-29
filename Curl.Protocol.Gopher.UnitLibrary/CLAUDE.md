# Curl.Protocol.Gopher.UnitLibrary

Phase 4.

Gopher document retrieval: `GopherProtocolHandler` sends the URL's selector followed by
CRLF and writes the server's reply to the output unaltered until the server closes the
connection, as curl 8.21.0 does. `GopherSelector` builds the selector: the path and
query as written (dot segments removed, still percent-encoded) less their first two
characters, then percent-decoded.

**URL schemes:** `gopher` (default port 70), `gophers` (default port 70, the same
handler with `ConnectTarget.UseTls` true)

**Seam:** `IConnector` (ADR-0005). The handler asks it for one connection per transfer
and disposes that connection itself; it never takes an `IConnection` in its constructor.
A transfer proxy (`ITransferContext.Proxy`) goes into that `ConnectTarget`, and the
connector tunnels through it (ADR-0056); the handler holds no proxy code.

Curl's own diagnostic log (`--log-level`, ADR-0222, BL-928): `GopherDiagnosticLog` writes
component `gopher` from `ITransferContext.DiagnosticLog` - the failure that ends a transfer
as `error` with its `CurlExitCode`, and the selector sent and the transfer's end (bytes
and milliseconds) as `info`; the connect target carries the log on.

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnector`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

The source of truth for behaviour is curl 8.21.0's `lib/gopher.c`, plus measurements
against the local curl 8.21.0; the tests name each measured case.
