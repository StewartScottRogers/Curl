# ADR-0042 — The sws emulation carries out timing commands on an injected clock and leaves `delay` unsupported

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (task BL-264, 2026-09-26).

## Context

`SwsHttpServerConnector` in `Curl.Conformance.UnitLibrary` emulates upstream's `sws`
(`tests/server/sws.c` at `curl-8_21_0`) in memory. BL-146 left the `<servercmd>` commands
`idle`, `stream`, `delay: N`, `writedelay: N`, `connection-monitor` and `upgrade`, and the
`<postcmd>` command `wait N`, unimplemented. In the vendored test data, `idle` is used by 1
case, `writedelay` by 12, `connection-monitor` by 11, `upgrade` by 29 (the WebSocket cases),
`<postcmd>` `wait` by 6, and `stream` and `delay` by none.

Read in `sws.c`:

- `idle` answers nothing and keeps reading requests; `stream` writes
  `a string to stream 01234567890\n` until a write fails and reads nothing more.
- Every reply goes out in writes of at most 20 bytes; `writedelay: N` sleeps N ms after each
  (after the only, empty, write of an empty reply too). `<postcmd>` `wait N` sleeps N seconds
  after the reply, before the connection is closed or read again.
- `connection-monitor` sets one server-wide flag when a request's `<servercmd>` is read;
  the next connection close, by either side, writes `[DISCONNECT]\n` to the request dump and
  clears it.
- `upgrade` makes a request with `Upgrade:` anywhere in it (case-sensitive, checked after
  `auth_required`, never for a chunked request) end at its headers. sws answers it, then, unless
  the reply closes the connection, reads raw traffic into the request dump until a one-second
  `select()` finds nothing, and closes.
- `delay: N` sleeps N ms after `accept()`, but only while `req->delay` is non-zero, and
  `init_httprequest` resets it after every request. With one client making one request at a
  time, which is how every test case runs, it is zero at every `accept()`: it takes effect only
  when a connection is accepted while another's request is part-read.

## Decision

1. `idle`, `stream`, `writedelay`, `connection-monitor`, `upgrade` and `<postcmd>` `wait` are
   carried out as read above, and are no longer listed in `UnsupportedServerCommands`.
2. Time comes from a `TimeProvider` given to `SwsHttpServerConnector` (the one-argument
   constructor uses `TimeProvider.System`). Each connection keeps a timeline of sends, each
   readable from the moment sws would write it; a read waits on the clock for the next one. A
   read that sws would leave waiting for ever (after `idle`) ends only by its cancellation
   token.
3. The 404 document for a malformed request line ignores `<servercmd>` and `<postcmd>`, as sws
   sends it before reading either.
4. `delay: N` stays listed in `UnsupportedServerCommands`. Emulating it faithfully needs sws's
   single-threaded interleaving of connections, which the emulation does not model, for an
   effect no vendored case depends on.

## Consequences

- A case using `delay:` is still skipped with a reason; none does at `curl-8_21_0`.
- The WebSocket cases' traffic is recorded raw, as sws dumps it; decoding it is the comparison
  stage's business.
- Tests drive the timing with a hand-written manual `TimeProvider`; no test waits on the real
  clock.
