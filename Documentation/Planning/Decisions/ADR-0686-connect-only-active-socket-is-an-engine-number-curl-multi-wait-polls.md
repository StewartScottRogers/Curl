# ADR-0686 — A connect-only connection's active socket is an engine number that curl_multi_wait polls

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2633.

## Context

Upstream test677 (`lib677.c`) performs an IMAP `CURLOPT_CONNECT_ONLY` 1 transfer on a multi handle, reads
`CURLINFO_ACTIVESOCKET` from the done message's handle, ends the program when it is `CURL_SOCKET_BAD`, and
otherwise passes that socket to `curl_multi_wait` as an extra socket, sending `A1 IDLE` with `curl_easy_send`
once the wait reports it writable and reading the reply once it reports it readable.

ADR-0549 answered -1 for `CURLINFO_ACTIVESOCKET` and `CURLINFO_LASTSOCKET` because the engine's connections
are managed streams behind `IConnection`, not operating system descriptors, and said connect-only work would
revisit it. With BL-2623 the IMAP connect-only perform keeps its connection, but lib677 still read -1 and
stopped before sending, so test677 failed after `A001 CAPABILITY`. In the conformance harness the connection
is an in-memory stream, so there is no descriptor to hand out even in principle.

## Decision

- **A connect-only connection gets a socket number of the engine's own.** `ConnectOnlyConnection.OpenAsync`
  numbers each connection it keeps from `ConnectOnlyConnection.FirstSocket` (`0x60000000`) upward, one counter
  for the process, apart from the multi handle's `FirstTransferSocket` (`0x40000000`) numbers. While the
  connection is open and the options still ask for connect-only, `ManagedEasyTransfer.Info` answers it as
  both `ActiveSocket` and `LastSocket`; otherwise both are -1, as before.
- **`curl_multi_wait` and `curl_multi_poll` answer that number from the connection.** For each extra socket,
  `ManagedMultiTransfer` first asks every `ManagedEasyTransfer` in the handle whether the number is its
  connect-only socket (`ConnectOnlyReadyEvents`); if so the answer is `ConnectOnlyConnection.ReadyEvents`:
  `POLLOUT` while no send is still running, `POLLIN` once bytes (or the peer's close) wait to be received,
  a wait for `POLLIN` starting the read a receive would. Any other socket still goes to `IWaitSocketPoller`.

The engine number is the simplest answer that keeps Curl a drop-in replacement through its own API: a
caller can only use the number with `curl_multi_wait`, `curl_multi_poll` and the info calls, and those now
answer it as libcurl answers its descriptor. Handing out the `Socket`'s real handle would not work for the
in-memory connections, TLS streams or proxied tunnels, and a caller's own `select` on it would bypass the
bytes the engine's TLS layer buffers.

## Consequences

- Upstream test677 passes and joins the ratchet.
- A number is recognised only while its transfer is in the multi handle waited on, as lib677 keeps it; a
  socket from a removed transfer is passed to the operating system poller like any foreign descriptor.
- `CURLOPT_CONNECT_ONLY` 2 (WebSocket) connections still answer -1; giving them a number is separate work.
- This amends ADR-0549's socket decision for connect-only connections.
