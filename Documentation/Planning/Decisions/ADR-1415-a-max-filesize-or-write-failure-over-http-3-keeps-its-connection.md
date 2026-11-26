# ADR-1415 — A --max-filesize or write failure over HTTP/3 keeps its connection

- **Status:** Accepted
- **Date:** 2026-11-25

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5996.

## Context

Only a timed-out (ADR-1410) or `-f` failed (ADR-1414) HTTP/3 transfer marked its stream done
and kept its connection. curl 8.18.0's ngtcp2 build was measured with
`Record-CurlExchange.ps1 -Http3StalledOrigin` and
`--http3-only -k -sS -v --trace-config http/3 -m 5 <url>/x <url>/y`, with `--max-filesize 1`
(exit 63) and with `-o` into a directory that does not exist (exit 23). Both write
`easy handle is done`, `no active streams, unset keep-alive`,
`query conn[0]: MAX_CONCURRENT -> 99 (0 in use)` and `Connection #0 to host ... left intact`,
reuse the connection for `/y`, and write `peer idle timeout` again before stream 4. A stream
left open is cancelled (STOP_SENDING `0x10c`); one whose end arrived with the answer
(`-Http3EndStream`) is closed with `quic close(app_error=256)` and nothing is sent for it.

## Decision

Over HTTP/3, exit 63 and exit 23 end only the stream, exactly as `-f` does (ADR-1414): Curl
reads what has already arrived, cancels the stream unless it ended both ways, marks the
transfer done and keeps the connection.

HTTP/2 keeps its behaviour for these two failures, as curl was not measured there.

## Consequences

- The next URL goes out on the same connection and writes curl's `--trace-config http/3` lines.
- Other HTTP/3 failures, such as a stream the server resets, still close the connection and do
  not mark the stream done: the recorder cannot make them, so curl is not yet measured.
- curl's `error <n> writing ... bytes`, `read_stream`, `xfer write failed` and `cf_recv` trace
  lines are still not written (ADR-1414).

## Alternatives considered

- **Marking every failed transfer done and keeping its connection:** pins behaviour nobody has
  measured, a lost connection's included.
