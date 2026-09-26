---
id: BL-032
title: Record ADR-0005 and ADR-0006 for transport connectors and the Phase 4 transfer options
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md, Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md, Documentation/Planning/Decisions/README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-09-26
completed:
---
# BL-032 — Record ADR-0005 and ADR-0006 for transport connectors and the Phase 4 transfer options

## Goal

Two new ADRs are recorded and indexed: ADR-0005 decides how a wire-protocol handler
obtains a transport for each transfer (`IConnector` for byte streams, `IDatagramConnector`
and `IDatagramChannel` for TFTP), and ADR-0006 decides the Phase 4 option members added
to `ITransferContext` and the `TransferContext` data class that protocol tests build
contexts with.

## Context

The two contract tasks that depend on this one implement these decisions, so the names
below are fixed here and must be recorded exactly.

**Why a connector.** `Documentation/Product/Product-Overview.md`, Rule 2, sketches
`FtpProtocolHandler(IConnection, IDnsResolver, TimeProvider)`: the connection is
constructor-injected. But the host and port come from each transfer's URL, and
`Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs` says handlers are registered
once and resolved as a set, so a handler cannot receive its `IConnection` at construction.
No contract today turns a host, a port and "secure or not" into an `IConnection`.

**Why a datagram seam.** `IConnection` is a byte stream. TFTP runs over UDP
(`Curl.Protocol.Tftp.UnitLibrary/CLAUDE.md`: "The only UDP protocol in the set"), where
datagram boundaries carry meaning and the server answers from a new port - its transfer
identifier, RFC 1350 section 4 - which the client must then address. A stream cannot
express either. This is the same shape of argument ADR-0002 made for `IFileSystem`.

**Why results, not exceptions.** Measured on 2026-09-26 with the local curl 8.21.0
(`x86_64-w64-mingw32`, Release-Date 2026-06-24):
`curl dict://nonexistent.invalid/d:x` exits 6 with
`Could not resolve host: nonexistent.invalid`, and `curl dict://127.0.0.1:1/d:x` exits 7
with `Failed to connect to 127.0.0.1:1 after 2013 ms: Could not connect to server`
(<https://curl.se/libcurl/c/libcurl-errors.html>). A failed connect is an expected
outcome with a curl exit code, the same situation `FileOpenResult` handles for
`IFileSystem`.

**The Phase 4 options**, all checked against curl 8.21.0 on 2026-09-26:

- `-d`/`--data` - "For MQTT, the data is sent as a PUBLISH"
  (<https://curl.se/docs/manpage.html#-d>; <https://curl.se/docs/mqtt.html>).
- `-u`/`--user` - measured: `curl -u bob:secret -d x mqtt://127.0.0.1:11883/t` sends a
  CONNECT whose flags byte is `0xC2`, followed by the user name `bob` and the password
  `secret`.
- `-t`/`--telnet-option` - `TTYPE`, `XDISPLOC` and `NEW_ENV`, as `<option=value>`
  (<https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html>). Measured: an unknown name is
  exit 48 and a value without `=` is exit 49, both reported at transfer time after the
  connection is made, so validation belongs to the telnet handler and the list travels
  unvalidated.
- `--tftp-blksize` - default 512, valid range 8-65464
  (<https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html>). Measured: 5 is sent as
  `blksize 8` and 70000 as `blksize 65464`, so the value is clamped, not refused.
- `--tftp-no-options` - suppresses the RFC 2347, 2348 and 2349 options
  (<https://curl.se/libcurl/c/CURLOPT_TFTP_NO_OPTIONS.html>).
- Telnet's input - "it sends what it reads on stdin" (<https://curl.se/docs/manpage.html>,
  TELNET) - is carried by the existing `ITransferContext.Upload`; no new member.

**Why `TransferContext`.** Every protocol test project would otherwise declare its own
`ITransferContext` implementation, as
`Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs` does. Tasks already on the
board add members to `ITransferContext` (BL-013, BL-020); each addition would then break
every protocol test project, which parallel dark factory lanes cannot see coming because
those projects are outside the adding task's `touches`. One settable data class in
`Curl.Protocol.Abstractions.UnitLibrary` makes a member addition a one-project change.

**Why a new ADR rather than an edit to ADR-0003.** ADR-0003 is Accepted, and
`Documentation/Planning/Decisions/README.md` says an Accepted ADR is immutable.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`
      exists in the template shape from `Decisions/README.md`, Status `Accepted`, dated
      the day it is written, and its Decision section names exactly:
      `IConnector.ConnectAsync(ConnectTarget target, CancellationToken cancellationToken)`
      returning `ValueTask<ConnectResult>`; `ConnectTarget(string Host, int Port, bool UseTls)`;
      `ConnectResult` with factories `Connected(IConnection connection)` and
      `Failed(CurlExitCode exitCode, string errorMessage)`;
      `IDatagramConnector.OpenAsync(string host, int port, CancellationToken cancellationToken)`
      returning `ValueTask<DatagramOpenResult>`, with factories
      `Opened(IDatagramChannel channel)` and `Failed(CurlExitCode exitCode, string errorMessage)`;
      `IDatagramChannel : IAsyncDisposable` with `EndPoint ServerEndPoint`,
      `SendAsync(ReadOnlyMemory<byte> datagram, EndPoint destination, CancellationToken cancellationToken)`
      returning `ValueTask`, and
      `ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)` returning
      `ValueTask<DatagramReceived>`, where `DatagramReceived(int Length, EndPoint RemoteEndPoint)`.
- [ ] ADR-0005 states that a connector returns every resolve, connect or TLS failure as
      a result carrying the curl exit code and message and lets only an
      `OperationCanceledException` escape; that handlers take connectors in their
      constructors and never construct a `Socket` or `SslStream`; that the production
      implementations live in `Curl.Networking.UnitLibrary`; and it quotes the exit 6
      and exit 7 measurements above with the curl version.
- [ ] `Documentation/Planning/Decisions/ADR-0006-transfer-context-carries-phase-4-protocol-options.md`
      exists in the same shape, and its Decision section names exactly these
      `ITransferContext` members: `ReadOnlyMemory<byte>? PostData` (`-d`; `null` when
      not given), `System.Net.NetworkCredential? Credentials` (`-u`, else the URL's user
      information; `null` when neither), `IReadOnlyList<string> TelnetOptions` (each `-t`
      value verbatim, in command-line order; empty when none), `int? TftpBlockSize`
      (`--tftp-blksize` as given, unclamped, `null` when not given; the TFTP handler
      clamps) and `bool TftpNoOptions` (`--tftp-no-options`).
- [ ] ADR-0006 also decides `TransferContext`: a sealed class in
      `Curl.Protocol.Abstractions.UnitLibrary` implementing `ITransferContext` with
      `init` properties, `Url` and `Output` `required`, `TimeProvider` defaulting to
      `TimeProvider.System`, and every other member defaulting to its "not given"
      value; and it states that protocol test projects build contexts with it instead of
      declaring their own `ITransferContext` implementation.
- [ ] ADR-0006 records the rejected alternatives - an untyped option bag (already
      rejected by ADR-0003) and a per-protocol context interface - each with its reason.
- [ ] The index table in `Documentation/Planning/Decisions/README.md` gains rows for 0005
      and 0006.
- [ ] Rule 2 in `Documentation/Product/Product-Overview.md` no longer shows a handler
      receiving `IConnection` in its constructor: it shows `IConnector`, names
      `IDatagramConnector` for TFTP, and points to ADR-0005.
- [ ] No `.cs` or project file is changed.

## Notes

If ADR-0005 or ADR-0006 is already taken when this runs, do not renumber anything: move
the task to `Blocked` saying which number is taken, because the file names are in this
task's `touches`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
