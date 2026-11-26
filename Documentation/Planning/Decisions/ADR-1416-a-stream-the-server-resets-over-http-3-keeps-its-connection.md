# ADR-1416 — A stream the server resets over HTTP/3 keeps its connection

- **Status:** Accepted
- **Date:** 2026-11-25

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5997.

## Context

ADR-1415 left a stream the server resets closing its connection, because it had not been
measured. `Record-CurlExchange.ps1 -Http3StalledOrigin -Http3EndStream -Http3ResetCode 0x102`
now resets the first request stream. curl 8.18.0's ngtcp2 build was measured with
`--http3-only -k -sS -v --trace-config http/3 -m 5 <url>/x <url>/y`, the reset sent before any
answer (`-Http3ResetBeforeAnswer`, exit 95), after the head with no body (exit 95) and after
body bytes (exit 18). All three write `[0] reset -> 0`, `[0] RESET: error 258`,
`[0] quic close(app_error=258) -> 0`, the failure's line, `[0] easy handle is done`,
`no active streams, unset keep-alive`, `query conn[0]: MAX_CONCURRENT -> 99 (0 in use)` and
`Connection #0 to host ... left intact`. `/y` reuses the connection, writes
`peer idle timeout` and goes out on stream 4, and succeeds. curl writes no later
`quic close(app_error=268)` for the reset stream.

## Decision

- A server reset that fails an HTTP/3 transfer (exit 95 or 18) ends only its stream: Curl
  writes the three reset lines and the failure as a `-v` line, marks the transfer done,
  reports the connection left intact and keeps it for the next transfer.
- The reset stream counts as ended, so nothing is sent for it. A request not yet fully sent
  is cancelled with `H3_REQUEST_CANCELLED`, as ngtcp2 shuts down a half still open; curl was
  not measured with an upload.
- The failure's text stays curl 8.21.0's, which names the error code (ADR-0187); 8.18.0 ends
  the same line at `reset by server`.
- A refused stream (`H3_REQUEST_REJECTED`) is unchanged: it stops the connection taking new
  streams (ADR-0187). A response head with no valid `:status`, which Curl itself resets, is
  unchanged too, as it was not measured.

## Consequences

- `-f`, `--max-filesize` or a failed write that finds the server's reset already arrived sends
  nothing for the stream, which ADR-1414 and ADR-1415 would otherwise cancel.
- This replaces ADR-1415's consequence that a reset stream closes the connection.

## Alternatives considered

- **Keeping the connection after every HTTP/3 failure:** pins behaviour nobody has measured, a
  lost connection's included.
