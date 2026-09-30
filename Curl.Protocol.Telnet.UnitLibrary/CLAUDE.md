# Curl.Protocol.Telnet.UnitLibrary

Phase 4.

Telnet, including option negotiation.

**URL schemes:** `telnet`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. The seam is
`IConnector` (ADR-0005): `TelnetProtocolHandler` takes one in its constructor and
asks it for the `IConnection` to each URL's host and port (23 by default), so the
tests in the matching `.UnitTests` project drive this code from a scripted byte
stream with no network. A transfer proxy (`ITransferContext.Proxy`) goes into that
`ConnectTarget`, and the connector tunnels through it (ADR-0056); the handler holds no
proxy code.

## Layout

- `TelnetProtocolHandler` runs the session: sends `ITransferContext.Upload` (each
  `0xFF` doubled) while writing received data to `Output`, until the server closes.
- `TelnetReceiver` turns received bytes into output data and replies, byte for byte
  as curl 8.21.0 does; it is pure, with no I/O.
- `TelnetOptionSide` is RFC 1143 option state for one side of the connection.
- `TelnetOptionParser` reads `ITransferContext.TelnetOptions` (`-t`) into
  `TelnetOptionValues` once connected, refusing a bad option with exit 48 or 49
  before a byte is sent. It first adds the `-u` user name as the NEW-ENVIRON variable
  `USER`, refusing a non-ASCII one with exit 43. `TTYPE`, `XDISPLOC` and `NEW_ENV` are negotiated; `BINARY=0`
  refuses BINARY both ways; `WS` makes this side offer NAWS and is the size sent once
  NAWS is agreed (0x0 without it, since curl agrees to NAWS regardless).

A connection read that fails ends the session with exit 0, a send that fails with
exit 55 and an output write that fails with exit 23, as curl 8.21.0 on Windows does
(measured in BL-077's Notes).

Curl's own diagnostic log (`--log-level`, ADR-0222, BL-928): `TelnetDiagnosticLog`
writes component `telnet` from `ITransferContext.DiagnosticLog` - the failure that ends
a session (a failed connect included) as `error` with its `CurlExitCode`, an option
request or offer refused as `warning`, the session's start (host:port) and end (bytes
and ms) as `info`, and each `WILL`/`WONT`/`DO`/`DONT` received and sent as `verbose`,
logged by `TelnetReceiver` and `TelnetOptionSide`. The `ConnectTarget` carries the log
on. None of it changes a byte sent, written or reported.

`-v` and `--trace` (BL-935): after connecting, `TelnetTraceReporter` reports to
`ITransferContext.Events` each negotiation received and sent (`RCVD DO TERM TYPE`),
any other command received (`RCVD IAC NOP`), each subnegotiation received and sent in
`printsub`'s one-line pieces, each run of output data between commands as data
received (never anything sent, and no zero-byte block at the close), then the failure's
message unless curl prints it without `failf`, and `closing connection #N` after exit 23
or `shutting down connection #N` after anything else.

Every byte these classes send or write was measured against curl 8.21.0; the
captures are in BL-043's, BL-044's, BL-077's, BL-083's, BL-084's and BL-085's Notes and pinned by `TelnetProtocolHandlerTests`,
`TelnetProtocolHandlerTelnetOptionTests`, `TelnetProtocolHandlerUserNameTests` and
`TelnetProtocolHandlerWindowSizeTests`. Change behaviour only against a new
measurement.
