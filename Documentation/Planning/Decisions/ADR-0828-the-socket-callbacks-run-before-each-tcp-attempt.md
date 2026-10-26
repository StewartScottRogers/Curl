# ADR-0828: The open-socket and socket-option callbacks run before each TCP attempt

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3565)
- Relates to: [ADR-0610](ADR-0610-socket-callbacks-over-opaque-connection-handles.md), [ADR-0818](ADR-0818-a-connect-asks-its-socket-opener-before-each-tcp-attempt.md)

## Context

CGR-036: `SocketCallbackConnector` let its inner connector connect and only then called
`CURLOPT_OPENSOCKETFUNCTION` and `CURLOPT_SOCKOPTFUNCTION` for the peer. So a `CURL_SOCKET_BAD`
came after the server had accepted a TCP connection, a socket the application returned was
ignored (the request went to the URL's host, not the socket's peer), and a connect to a closed
port called neither the open nor the close callback. curl 8.22.0's `cf_socket_open` calls the
open-socket callback for each address before the connect, connects the socket it returns, calls
the socket-option callback before connecting (skipping the connect on
`CURL_SOCKOPT_ALREADY_CONNECTED`), and calls the close-socket callback for each attempt's socket
that fails. ADR-0818 (BL-3833) gave `ConnectTarget` a per-attempt `ITcpSocketOpener` for this.

## Decision

`SocketCallbackConnector.ConnectAsync` puts an `AttemptSocketOpener` on the target. For each TCP
attempt it calls the open-socket callback with the attempt's address (`SOCK_STREAM`,
`IPPROTO_TCP`), else numbers the socket from `CurlSocketHandles`, then the socket-option callback:

- `CURL_SOCKET_BAD` refuses the attempt (no socket, no connection); the connect fails with exit 7
  and `Failed to connect to <host>:<port> after <n> ms: Could not connect to server`.
- The socket-option callback's error calls the close-socket callback and refuses the attempt; the
  connect fails with exit 42 and `Operation was aborted by an application callback`. Other
  addresses may still be tried by the race, where curl stops at once; no measured case tells them
  apart, as both end with exit 42 and no connection.
- A socket the open-socket callback returned is handed to the dialer as `Supplied` (connected by
  it, or used as it is under `CURL_SOCKOPT_ALREADY_CONNECTED`); without an open-socket callback the
  dialer opens its own (`Proceed`).
- An attempt whose connect fails (the opener told `SocketClosed`) has its socket closed through
  the close-socket callback; when the whole connect fails after an attempt connected (a TLS or
  tunnel failure), the connector closes that attempt's socket the same way. The winning attempt's
  socket is the `SocketCallbackConnection`'s, closed once when the connection is disposed.

Under an `EngineDialing` (the conformance harness's in-memory servers) the callback's socket is not handed
to the dialer, which opens its own: the upstream libtests return socket numbers no operating system
made (lib500, lib585, lib1960), and the in-memory dialer could not use one anyway.

A connector that never asks the opener (a Unix domain socket, test fakes) keeps the earlier
after-connect steps, and QUIC's UDP socket still gets them after the connect: neither goes through
the TCP address race.

## Consequences

- C_45, C_46 and C_47 of CGR-036 now behave as libcurl 8.22.0: no connection on a refusal, the
  request on the application's socket, and one open and one close for a refused port.
- `Cgr036Tests` pins each against a real `TcpConnector` on loopback sockets that send no byte.
