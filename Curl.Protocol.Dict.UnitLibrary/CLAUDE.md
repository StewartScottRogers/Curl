# Curl.Protocol.Dict.UnitLibrary

Phase 4.

Dictionary lookups.

**URL schemes:** `dict`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. The seam is
`IConnector` (ADR-0005): `DictProtocolHandler` takes one in its constructor and
asks it for the `IConnection` to each URL's host and port (2628 by default), so
the tests in the matching `.UnitTests` project drive this code from a scripted
byte stream with no network. A transfer proxy (`ITransferContext.Proxy`) goes
into that `ConnectTarget`, and the connector tunnels through it (ADR-0056); the
handler holds no proxy code.

## Layout

- `DictProtocolHandler` runs the transfer: connects, sends the request whole,
  and writes everything the server sends to `Output` until it closes.
- `DictRequest` encodes a URL path as the request bytes (`CLIENT`, one `DEFINE`,
  `MATCH` or raw command line, `QUIT`); it is pure, with no I/O. Its
  `ClientLine` constant names `libcurl 8.21.0`, as upstream sends it.
- `DictDiagnosticLog` writes Curl's own diagnostic log (`--log-level`, ADR-0222,
  BL-928), component `dict`, from `ITransferContext.DiagnosticLog`: the failure
  that ends a transfer as `error` with its `CurlExitCode`, and the command line
  sent and the transfer's end (bytes and milliseconds, timed with the context's
  `TimeProvider`) as `info`. The connect target carries the log on.
- `-v` and `--trace` (BL-934): after connecting, `DictProtocolHandler` reports the
  whole request to `ITransferContext.Events` as one block of data sent, each read as
  data received, the server's close as a zero-byte block, and then
  `shutting down connection #N`, which a refused path reports too.
- `DictIoFailures` turns a failed send, receive or output write into curl's exit
  55, 56 or 23 and its message (BL-1125), wording any socket error with the
  shared `CurlSocketErrorText` table (BL-1337); the handler returns it rather than
  throwing, and `-v` then shows the message (unless it is curl's fallback text),
  `Failed sending DICT request` after a failed send, and `closing connection #N`.

Every byte these classes send was measured against curl 8.21.0; the captures are
in BL-041's Notes and pinned by `DictProtocolHandlerTests`. Change behaviour only
against a new measurement.
