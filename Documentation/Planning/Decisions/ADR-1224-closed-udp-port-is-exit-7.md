# ADR-1224: A closed UDP port ends a transfer with exit 7

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4654), 2026-11-03.

## Context

On Windows an ICMP port unreachable reaches a UDP socket as `SocketError.ConnectionReset` on the
next receive. TFTP already maps it (curl's Windows build prints `curl: (7) Received too short packet`,
measured with `Record-CurlExchange.ps1` on the Windows (Schannel) curl), but `coap`, `dns` and `webtransport`
let the `SocketException` escape to the console's guard, which printed it as exit 2.

## Decision

`UdpDatagramConnector` wraps every channel it opens in `PortUnreachableReportingDatagramChannel`,
which turns a `ConnectionReset` or `ConnectionRefused` from a send or receive, only until the channel has received its first datagram (BL-4675; a port that refuses after the server has answered passes through unchanged for the handler), into a
`DatagramPortUnreachableException` carrying the connector's own exit 7 text,
`Failed to connect to <host>:<port>[ via <host>:<port>] after <n> ms: Could not connect to server`,
`n` measured from the open. The exception derives from `SocketException` and keeps the socket error,
so TFTP's existing catch answers as before. The engine's `EndPointReportingProtocolHandler`, which
wraps every handler, ends a transfer that lets one escape with exit 7 and its text.

curl has no `coap`, `dns` or `webtransport` scheme, and this Windows curl has no HTTP/3, so the text
is the UDP connector's existing exit 7 text rather than a measured one.
