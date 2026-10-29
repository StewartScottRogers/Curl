# ADR-0983: A socket-action transfer's look-up waits readable on a loopback wakeup socket the lookup's end signals

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4036, for CGR-034).

## Context

curl 8.22.0 with the threaded resolver tells the socket callback the resolver's wakeup descriptor (one end of a
socket pair) with `CURL_POLL_IN` while a name is looked up, and the resolver thread writes to it when the lookup
ends, so an application driving `curl_multi_socket_action` is woken by a socket. The managed engine's
`TransferSocketWait` had no resolve phase, so a resolving transfer had no told socket and
`ManagedMultiTransfer` fell back to its 10 ms `RunningTransferCheckInterval` timer.

## Decision

- `EngineComposition.CreateTransports` puts a `SocketActionGatedResolver` beneath `wrapLookUps` when it is given
  a `TransferSocketWait`. Each look-up opens a `LoopbackLookupWakeup`, records it with
  `TransferSocketWait.BeginResolve` (the transfer's `Socket` is its handle and `Waiting` is `CurlPoll.In`), and
  when the lookup ends, whatever its outcome, signals it and calls `EndResolve`.
- The wakeup is a real operating system socket, so an application's `poll` or `select` on it works: a TCP socket
  listening on `127.0.0.1` port 0, made readable by connecting a second socket to it, so no byte is sent
  (Windows' curl emulates its socket pair over loopback TCP too). A real socket was chosen over an engine
  socket number (ADR-0955) because an application polls what it is told, and a number no poll accepts would
  fail its `select`.
- The wakeup stays the transfer's `Socket` until the dial names another (`ConnectingOn`, or `TakeSocket`, which
  replaces it), and is disposed when a later look-up replaces it or `StopGating` runs, so an application never
  polls a closed handle it has not been told to remove. An action on it signals nothing the transfer's reads
  or writes would take, as it only wakes the application.
- The look-up itself is not held for the application's action: the transfer goes on to its dial as soon as the
  lookup ends. The callback order against real curl is pinned by BL-3990, not here.
