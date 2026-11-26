# ADR-1413: A transfer -f fails over HTTP/3 keeps its connection

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-25
- Task: BL-5983
- Replaces ADR-1410's "`-f` over HTTP/3 is unchanged until measured" and ADR-1408's
  "HTTP/1.x and HTTP/3 are unchanged" for HTTP/3.

## Context

Curl wrote `closing connection #0` after `-f` failed an HTTP/3 transfer, sent nothing for the
stream and dialled a new connection for the next URL. Measured with curl 8.18.0 (ngtcp2,
nghttp3, WinGet), `Record-CurlExchange.ps1 -Http3StalledOrigin` (with `-Http3EndStream` for
the ended cases), `--http3-only -k -sS -v --trace-config http/3 -f -m 5 <url>/x <url>/y`, each
stream answered `500`:

| Stream after the `500` head          | curl sends for the stream                                      | `--trace-config http/3`, besides the lines of every case |
|--------------------------------------|----------------------------------------------------------------|----------------------------------------------------------|
| head only, left open                 | STOP_SENDING 0x10c, Stream Cancellation (`40`, then `44`)      | `[0] quic close(app_error=268) -> 0` in the next transfer |
| head and DATA, left open             | the same                                                       | the same                                                 |
| head ends it                         | nothing                                                        | `[0] CLOSED`, `[0] quic close(app_error=256) -> 0`       |
| head, then DATA ending it            | nothing                                                        | the same                                                 |

In all four, exit 22, `easy handle is done`, `no active streams, unset keep-alive`,
`query conn[0]: MAX_CONCURRENT -> 99 (0 in use)`, `Connection #0 to host ... left intact`,
`Reusing existing https: connection with host 127.0.0.1`, and the second request on stream 4
of the same connection.

## Decision

A transfer `-f` fails on an HTTP/3 stream ends only its stream, as one `-m` ends does
(ADR-1410): the stream is cancelled unless it has ended both ways, the transfer is marked done,
and the connection is reported left intact and reused while its session takes new streams.

curl takes in the frames that arrived with the head before it gives the stream up, so Curl
first reads whatever has already arrived, without waiting, and drops the body
(`Http3StreamConnection.ReadArrivedResponseAsync`): a read that does not complete at once is
cancelled. A response whose end has arrived is then closed and nothing is sent for it. A read
that fails there leaves exit 22 and the stream is cancelled.

## Consequences

Whether the end counts as arrived depends on what the network has delivered by then, as it
does in curl. curl's other trace lines about the failed write (`error 22 writing 2 bytes of
headers`, `xfer write failed`, `cf_recv(...) -> 22`) are not part of this decision.
