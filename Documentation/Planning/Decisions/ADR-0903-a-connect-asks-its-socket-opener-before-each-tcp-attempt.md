# ADR-0903: A connect asks its socket opener before each TCP attempt

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3833
- Decided by Claude under Stewart's delegation.

## Context

Third-party gap report issue CGR-036: libcurl's `CURLOPT_OPENSOCKETFUNCTION` runs in curl 8.22.0's
`cf_socket_open`, before the connect, for every address the connect tries (a proxy's included), and
`CURLOPT_CLOSESOCKETFUNCTION` runs when an attempt's socket closes. The engine's
`SocketCallbackConnector` could only call the open-socket callback after the inner connector had
connected, because `TcpConnector`'s address race (`AddressFamilyRace`) opens the sockets and nothing
per transfer reached it before a dial. So a refusal came after the server had accepted a connection,
a socket the application made was ignored, and a failed connect called neither callback.

## Decision

- `Curl.Protocol.Abstractions` gains `ITcpSocketOpener` (`Open(IPEndPoint)` giving a
  `TcpSocketOpenDecision`, and `SocketClosed(IPEndPoint)`) and `ConnectTarget.SocketOpener`, `null` by
  default.
- `TcpSocketOpenDecision` is `Proceed` (the connector opens its own socket), `Refuse` (libcurl's
  `CURL_SOCKET_BAD`: the attempt fails with a `SocketException` before any socket opens, so with every
  address refused the connect ends with exit 7 and no TCP connection is made) or
  `Supplied(handle, alreadyConnected)`.
- `TcpConnector` wraps the race's dialer in `SocketOpenerTcpDialer` when the target has an opener, so
  `AddressFamilyRace` is unchanged. A supplied handle is adopted by `TcpDialer.DialSuppliedSocketAsync`
  as `new Socket(new SafeSocketHandle(handle, ownsHandle: false))` and connected unless already
  connected; `TcpDialer` stays the one type that constructs a TCP `Socket`. The connector never closes
  the handle: the application owns it, as with libcurl's close-socket callback.
- `SocketClosed` is called once for an attempt whose dial failed or was abandoned after its socket was
  opened (never for a refused one, which opened none). Closing a connection that succeeded is left to
  the connection's owner (BL-3565 calls the close-socket callback there).

## Consequences

- BL-3565 can switch `SocketCallbackConnector` to set the opener, running the open-socket and
  socket-option callbacks before the connect and the close-socket callback for a failed attempt.
- A connect with no opener is unchanged.
