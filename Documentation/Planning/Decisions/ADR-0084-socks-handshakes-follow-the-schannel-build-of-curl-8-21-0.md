# ADR-0084 — The SOCKS handshakes follow the Schannel build of curl 8.21.0

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-213;
recorded in BL-355.

## Context

BL-213 implemented SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h tunnels in
`Curl.Networking.UnitLibrary` (`SocksProxyTunnel`, `Socks4Handshake`, `Socks5Handshake`,
driven by `TcpConnector`). It measured curl 8.21.0 (x86_64-w64-mingw32, Schannel) byte for
byte against a scripted loopback proxy; the commands, bytes and messages are in BL-213's
Notes. The measurement settled the wire format, but left some behaviour to choose: what to
do with a method the reference build offers but this project does not implement, which
resolved address to send, what counts as an address literal, how text is encoded, and what
happens to the proxy connection when the handshake throws.

## Decision

1. **The SOCKS5 greeting offers GSSAPI, which is not implemented.** The greeting is
   `05 02 00 01`, or `05 03 00 01 02` with a credential, because the reference build sends
   exactly those bytes. A proxy that picks GSSAPI (`01`) fails with exit 97 and the message
   the reference build's SSPI printed against a loopback proxy with no Kerberos service
   name: `SSPI error: InitializeSecurityContext failed: SEC_E_TARGET_UNKNOWN (0x80090303) -
   The specified target is unknown or unreachable`.
2. **SOCKS4 sends the first IPv4 address resolved** (by the resolver or `--resolve`). When
   none is IPv4, it fails with exit 97 `SOCKS4 connection to <first address> not supported`.
   That message was measured only for an IPv6 literal (`::1`); a name that resolves only to
   IPv6 gets the same message with its first address.
3. **SOCKS5 sends the first address resolved**, IPv4 or IPv6, as address type `01` or `04`.
4. **A host is an address literal only when `IPAddress.TryParse` accepts it and, for IPv4,
   the address prints back identically** (`SocksProxyTunnel.ParseAddressLiteral`). Anything
   else, such as `1`, which `TryParse` reads as `0.0.0.1`, is a name: resolved locally for
   SOCKS4 and SOCKS5, sent as written for SOCKS4a and SOCKS5h. The literals measured
   (`127.0.0.1` and `::1` under SOCKS5h) are sent as addresses, as curl sends them; the
   shorthand forms were not measured, and a host written as `1` is far more likely a name
   than an address.
5. **User names, passwords and host names are sent as UTF-8.** Every byte measured was
   ASCII, where UTF-8 is the same bytes curl sends; curl sends the command line's bytes
   unchanged, and UTF-8 is what those bytes are on a UTF-8 terminal and in scripts.
6. **An exception while the handshake is sent or read disposes the proxy connection and
   propagates**, as the HTTP CONNECT path does (ADR-0023). A refused or cut-short handshake
   is not an exception: it returns exit 97 with curl's message and also disposes the
   connection.

## Consequences

- The greeting's bytes match the reference build, so a proxy that logs or checks the
  offered methods sees the same thing from either binary.
- A SOCKS5 proxy that requires GSSAPI cannot be used; the user gets the reference build's
  message for the case it measured, not a working tunnel. Implementing GSSAPI would be new
  work with its own task.
- Choosing the first address, rather than trying each in turn, means one refused CONNECT
  ends the transfer, as it does in curl.
- The literal test rejects IPv4 shorthand, so `socks4a://` and `socks5h://` pass such a host
  to the proxy as a name, and the proxy decides what it means.
- A name or credential outside ASCII is sent as UTF-8 even where a non-UTF-8 code page would
  have given curl other bytes; that case was not measured.

## Alternatives considered

- **Offer only no authentication and user name and password in the SOCKS5 greeting.** This
  lost because the greeting's bytes would differ from the reference build's, which a
  drop-in replacement must not do for a case that works.
- **Implement GSSAPI through SSPI.** This lost for now: it is Windows-only, needs a Kerberos
  environment to measure, and is not needed by any case measured to work.
- **SOCKS4 sends the first address whatever its family.** This lost because SOCKS4 carries
  only four address bytes; curl refuses instead, and so does this project.
- **Treat anything `IPAddress.TryParse` accepts as a literal.** This lost because `TryParse`
  accepts `1` and `127.1`, which would send a host name the user wrote as a name as an
  address.
- **Encode as Latin-1 or the console's code page.** This lost because it gives the same
  bytes for ASCII and worse ones everywhere .NET strings hold text beyond it.
- **Leave the proxy connection open after an exception for the caller to close.** This lost
  because the caller never receives it; `TcpConnector` owns it until the tunnel opens.
