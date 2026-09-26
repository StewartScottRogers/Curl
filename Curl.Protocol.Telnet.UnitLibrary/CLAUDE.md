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
stream with no network.

## Layout

- `TelnetProtocolHandler` runs the session: sends `ITransferContext.Upload` (each
  `0xFF` doubled) while writing received data to `Output`, until the server closes.
- `TelnetReceiver` turns received bytes into output data and replies, byte for byte
  as curl 8.21.0 does; it is pure, with no I/O.
- `TelnetOptionSide` is RFC 1143 option state for one side of the connection.
- `TelnetOptionParser` reads `ITransferContext.TelnetOptions` (`-t`) into
  `TelnetOptionValues` once connected, refusing a bad option with exit 48 or 49
  before a byte is sent. `TTYPE`, `XDISPLOC` and `NEW_ENV` are negotiated; `WS` and
  `BINARY` are checked as curl checks them and otherwise ignored.

A connection read that fails ends the session with exit 0, a send that fails with
exit 55 and an output write that fails with exit 23, as curl 8.21.0 on Windows does
(measured in BL-077's Notes).

Every byte these classes send or write was measured against curl 8.21.0; the
captures are in BL-043's, BL-044's and BL-077's Notes and pinned by `TelnetProtocolHandlerTests`
and `TelnetProtocolHandlerTelnetOptionTests`. Change behaviour only against a new
measurement.
