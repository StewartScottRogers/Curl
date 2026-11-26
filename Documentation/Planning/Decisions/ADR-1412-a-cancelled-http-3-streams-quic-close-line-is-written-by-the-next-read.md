# ADR-1412: A cancelled HTTP/3 stream's quic close line is written by the next read

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5982
- Completes ADR-1410's missing trace line.

## Context

Under `--trace-config http/3`, curl writes `[HTTP/3] [0] quic close(app_error=268) -> 0` for
a stream a time limit cancelled, once ngtcp2 closes it, among the lines of whichever
transfer is then reading packets. Measured with curl 8.18.0 (ngtcp2 1.21.0) against
`Record-CurlExchange.ps1 -Http3StalledOrigin`,
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`, four runs:

- Stream 0's line, written during `/y`: in all four, after `Request completely sent off` and
  before `< HTTP/3 200`.
- Stream 4's line, written during `/z`: in some runs before `[8] OPENED stream`. Where it
  falls depends on when the server's reply to STOP_SENDING arrives.

`IMultiplexedStream` does not say when QUIC closes a stream Curl has aborted.

## Decision

- `Http3Session` keeps each stream cancelled before its response ended, and the next read of a
  response on the connection writes its line before it takes any frame: after
  `Request completely sent off` and before the status line, where curl writes it on the first
  reuse in every run.
- Curl does not wait for QUIC to close the stream. Against a server that never answers the
  STOP_SENDING, curl would write no line and Curl writes one.
- A stream whose response had ended (a status that stopped the upload, ADR-1411) is not kept:
  its `quic close(app_error=256)` line is already written.

## Consequences

The line's position matches curl's most common one without a new member on the QUIC stream
contract. No next transfer, no line, as with curl.
