# ADR-0005 — Protocol handlers acquire transports through connectors

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

`Documentation/Product/Product-Overview.md`, Rule 2, sketched a handler as
`FtpProtocolHandler(IConnection, IDnsResolver, TimeProvider)`: the connection
constructor-injected. That cannot work. The host and the port come from each
transfer's URL, and `Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs` says
handlers are registered once and resolved as a set, so a handler exists before any
URL does and cannot be handed its `IConnection` at construction. No contract today
turns a host, a port and "secure or not" into an `IConnection`.

TFTP adds a second problem. `IConnection` is a byte stream. TFTP runs over UDP
(`Curl.Protocol.Tftp.UnitLibrary/CLAUDE.md`: "The only UDP protocol in the set"), where
datagram boundaries carry meaning and the server answers from a new port - its
transfer identifier, RFC 1350 section 4 - which the client must then address. A stream
can express neither. This is the same shape of argument ADR-0002 made for
`IFileSystem`: a seam that does not fit the medium is replaced by one that does, not
stretched.

Failing to reach a server is an expected outcome with its own curl exit code, not an
exceptional one. Measured on 2026-09-26 with the local curl 8.21.0
(`x86_64-w64-mingw32`, Release-Date 2026-06-24):

- `curl dict://nonexistent.invalid/d:x` exits 6 with
  `Could not resolve host: nonexistent.invalid`.
- `curl dict://127.0.0.1:1/d:x` exits 7 with
  `Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server`.

(<https://curl.se/libcurl/c/libcurl-errors.html>.) This is the situation
`FileOpenResult` already handles for `IFileSystem`.

## Decision

A wire-protocol handler takes a **connector** in its constructor and asks it for a
transport once per transfer, from the host and port in that transfer's URL. The
following types are added to `Curl.Protocol.Abstractions.UnitLibrary`.

For byte-stream protocols:

- `IConnector` with
  `ValueTask<ConnectResult> ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)`.
- `ConnectTarget(string Host, int Port, bool UseTls)` - a record naming what to
  connect to and whether to wrap the connection in TLS.
- `ConnectResult`, created only through its factories
  `Connected(IConnection connection)` and
  `Failed(CurlExitCode exitCode, string errorMessage)`.

For TFTP, the one datagram protocol:

- `IDatagramConnector` with
  `ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken)`.
- `DatagramOpenResult`, created only through its factories
  `Opened(IDatagramChannel channel)` and
  `Failed(CurlExitCode exitCode, string errorMessage)`.
- `IDatagramChannel : IAsyncDisposable` with
  - `EndPoint ServerEndPoint` - the resolved endpoint the first datagram goes to;
  - `ValueTask SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)`;
  - `ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)`.
- `DatagramReceived(int Length, EndPoint RemoteEndPoint)` - how many bytes of the
  buffer one datagram filled, and where it came from, so the handler can learn and
  then enforce the server's transfer identifier.

Rules every connector follows:

- A connector returns every resolve, connect or TLS failure as a `Failed` result
  carrying the curl exit code and the message curl prints (exit 6 and exit 7 above
  being the common cases). Only an `OperationCanceledException` escapes it.
- Handlers take connectors in their constructors and never construct a `Socket` or
  `SslStream`; the handler owns and disposes the `IConnection` or `IDatagramChannel`
  it was given for that transfer.
- The production implementations of `IConnector` and `IDatagramConnector` live in
  `Curl.Networking.UnitLibrary`. A unit test supplies a fake connector that hands back a
  fake connection replaying recorded bytes, or a fake `Failed` result.

## Consequences

Good:

- A handler stays a stateless singleton, registered once, while each transfer gets
  its own transport for its own host and port.
- Connect failures are asserted like any other result: a test hands the handler a
  connector returning `Failed(CurlExitCode.CouldntConnect, ...)` and checks the exit
  code, with no socket and no timeout.
- TFTP's port change and datagram framing are expressible, and testable as sequences
  of datagrams with their source endpoints.
- DNS, TLS and socket options stay in one place, `Curl.Networking.UnitLibrary`, rather
  than being reassembled by each handler from `IDnsResolver` and `ITlsProvider`.

Costs and caveats:

- Two transport seams instead of one; a protocol author must pick the right one.
  Every protocol but TFTP uses `IConnector`.
- Connection reuse across transfers is now the connector's business and is not
  decided here.
- Protocols whose second connection is negotiated mid-session (FTP's data connection)
  call the same `IConnector` again with the negotiated host and port; the connector
  does not know why it is asked.

## Alternatives considered

- **Inject `IConnection` into the handler's constructor**, as Rule 2 sketched. Rejected:
  the handler is built before any URL is known, so there is nothing to connect to.
- **Pass an `IConnection` in through `ITransferContext`.** Rejected: the command-line
  layer would have to know each scheme's default port, whether it starts in TLS, and
  that FTP needs a second connection; that knowledge belongs to the handler.
- **Throw on connect failure.** Rejected: an unreachable host is an ordinary outcome
  with an ordinary exit code, and exceptions would push every handler into
  catch-and-translate code for it, as `FileOpenResult` already avoids for files.
- **Model TFTP over `IConnection`.** Rejected: a byte stream loses datagram boundaries
  and cannot say which endpoint a datagram came from or should go to.
