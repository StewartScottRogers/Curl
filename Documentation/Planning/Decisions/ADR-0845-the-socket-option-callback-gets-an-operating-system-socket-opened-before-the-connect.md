# ADR-0845 — The socket-option callback gets an operating system socket opened before the connect

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3674, CGR-145).

## Context

CGR-145 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) measured libcurl 8.22.0 (Schannel,
Windows) with a `CURLOPT_SOCKOPTFUNCTION` and no `CURLOPT_OPENSOCKETFUNCTION`: the callback gets the
real socket `cf_socket_open` made before the connect (`getsockopt(SO_TYPE)` works on it);
`CURL_SOCKOPT_ERROR` makes no connection (exit 42); `CURL_SOCKOPT_ALREADY_CONNECTED` skips the connect,
so the first send on the unconnected socket fails (exit 55) and the server sees no connection.
Since ADR-0818 the callbacks run before each attempt dials, but without an open-socket callback the
socket-option callback got an engine number from `CurlSocketHandles` (`setsockopt` on it fails) and
`AlreadyConnected` was ignored, the dialer connecting its own socket.

## Decision

1. When the socket-option callback is set, the open-socket callback is not, and the dialer adopts
   sockets (no `EngineDialing`), `SocketCallbackConnector.AttemptSocketOpener` opens a TCP socket of the
   attempt's family itself, with `TCP_NODELAY` (libcurl's default `CURLOPT_TCP_NODELAY` 1), before the
   attempt dials. Its handle goes to the socket-option callback and, for an attempt that goes on, to
   the dialer as `TcpSocketOpenDecision.Supplied`, already connected under `AlreadyConnected` and so
   used as it is.
2. The engine owns that socket: its `CurlSocketCloser` holds it and closes it after the close-socket
   callback (if any), marked `IsTheConnectionsOwnSocket` so the callback runs before the connection
   closes, as ADR-0842 has it for the dialer's own socket. A refused or failed attempt closes it the
   same way.
3. Only the socket-option callback triggers this: with only a close-socket callback the dialer still
   opens its own socket (ADR-0842), so `--tcp-nodelay`, keepalive and the other dialer options keep
   applying there. Under this decision a supplied engine socket gets `TCP_NODELAY` only, as a socket
   the open-socket callback supplies gets none.
4. `Curl.Networking`'s `DeferredConnectSocketStream.WriteAsync` wraps a `SocketException` in an
   `IOException`, as `NetworkStream` does, so the HTTP handler fails a send on the unconnected socket
   with exit 55 instead of letting the exception escape the perform.

## Consequences

The probe of CGR-145 case C_43 gives libcurl's results on Windows: `Ok: rc=0 real=True`,
`Error: rc=42 serverconns=0`, `AlreadyConnected: rc=55 reqs=0 serverconns=0`. The conformance
harness's in-memory dialing keeps the engine numbers, as libtests return sockets no operating system
made.
