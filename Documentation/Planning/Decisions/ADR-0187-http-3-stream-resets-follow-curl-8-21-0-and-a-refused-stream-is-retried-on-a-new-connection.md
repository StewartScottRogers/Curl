# ADR-0187 — HTTP/3 stream resets follow curl 8.21.0, and a refused stream is retried on a new connection

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-839.

## Context

ADR-0144 section 7 and ADR-0172 section 6 took HTTP/3's unmeasured failure texts from
`lib/vquic/curl_ngtcp2.c` at `curl-8_18_0`, because curl.se's Windows build 8.18.0 (ngtcp2
1.21.0, nghttp3 1.15.0), installed by WinGet, was the only HTTP/3-capable curl on the
measuring host. How curl handles a reset request stream (`recv_closed_stream`) changed after
that release:

- **`curl-8_18_0`:** every reset is `failf(data, "HTTP/3 stream %" PRId64 " reset by
  server", stream->id)`, returning `CURLE_PARTIAL_FILE` (18) once body bytes arrived
  (`data->req.bytecount`) and `CURLE_HTTP3` (95) before. No case for
  `H3_REQUEST_REJECTED`, no retry. BL-731 built this
  (`HttpTransferMessages.Http3StreamReset`, used by
  `Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs`).
- **`curl-8_19_0` and later**, including `curl-8_21_0`, where the HTTP/3 half of the old
  file is now `lib/vquic/cf-ngtcp2.c` and the QUIC half `lib/vquic/cf-ngtcp2-cmn.c`: a
  reset with `H3_REQUEST_REJECTED` is retried on a new connection, a reset after the
  complete head of a `-I` response is ignored, and every other reset names its error code.

Everything else about Curl already claims one release: `-V` prints `curl 8.21.0`
(`Curl.Cli.UnitLibrary/CurlVersionText.cs`), the request's `User-Agent` is `curl/8.21.0`,
the Windows reference build is Git for Windows' curl 8.21.0 (ADR-0018), and the alt-svc
and HSTS caches follow libcurl 8.21.0 (ADR-0175, ADR-0179). That build has no HTTP/3, which
is why ADR-0144 measured 8.18.0, but an HTTP/3 transfer that sends `curl/8.21.0` and then
fails as 8.18.0 does would be two releases in one binary.

`curl-8_21_0`'s source was read for this decision (2026-09-29): `recv_closed_stream` and
`cb_h3_stream_close` in `lib/vquic/cf-ngtcp2.c`, `vquic_h3_err_str` and
`Curl_conn_may_http3` in `lib/vquic/vquic.c`, the `CURL_H3_ERR_*` codes in
`lib/vquic/vquic_int.h`, `Curl_retry_request` and `Curl_pretransfer` in `lib/transfer.c`,
and the `MSTATE_PERFORMING` step of `lib/multi.c`. It cannot be measured: curl.se's 8.21.0
build is not installed, and no local HTTP/3 server can be made to reset a stream on
demand (ADR-0144). The texts below are therefore from the source, and the task that
measures against a failing HTTP/3 server replaces any that differ.

## Decision

Curl's HTTP/3 behaviour follows **`curl-8_21_0`**, the release Curl reports itself as.
Its reset handling is `recv_closed_stream` in `lib/vquic/cf-ngtcp2.c` at that tag:

1. **What counts as a reset.** A request stream closed with an application error code
   other than `H3_NO_ERROR` (`0x100`) is reset (`cb_h3_stream_close`). A stream closed with
   `H3_NO_ERROR` is a clean close, and one that ends before the complete response head
   keeps ADR-0172's `HTTP/3 stream <id> was closed cleanly, but before getting all
   response header fields, treated as error`, exit 95.
2. **A reset with `H3_REQUEST_REJECTED` (`0x10b`)** is checked first, before the `-I`
   rule. curl prints the info line (a `*` line under `-v`)
   `HTTP/3 stream <id> refused by server, try again on a new connection`, marks the
   connection closed so no further request uses it, and fails the read with
   `CURLE_RECV_ERROR`. `Curl_retry_request` then runs:
   - **If nothing of the response has arrived** (no header or body bytes), it prints
     `REFUSED_STREAM, retrying a fresh connect` and, while fewer than 5 retries have run,
     `Connection died, retrying a fresh connect (retry count: <n>)` with `<n>` from 1 to
     5, closes the connection and sends the same request again, body rewound, over a new
     connection. The new connection is chosen as the first one was, so under `--http3`
     it races again and may end up on TCP.
   - **The retry is curl's own**, independent of `--retry`: it runs without `--retry`,
     and it does not consume `--retry`'s count. Its budget is `CONN_MAX_RETRIES`, 5,
     counted per transfer (reset in `Curl_pretransfer`, so shared across that transfer's
     redirects and with curl's retry of a reused connection that died).
   - **When the sixth attempt is refused**, curl prints the refused line and
     `REFUSED_STREAM, retrying a fresh connect`, then fails with
     `Connection died, tried 5 times before giving up`. The transfer's exit is **56**
     (`CURLE_RECV_ERROR`), not the 55 `Curl_retry_request` returns: `multi.c` keeps the
     read's error when it is already set. That message is the transfer's error text,
     since the refused path prints no error of its own.
   - **If header or body bytes had arrived** before the refusal, there is no retry: the
     transfer fails with exit 56 and, as no error was printed, curl's generic message for
     56, `Failure when receiving data from the peer`.
3. **A reset after the complete response head when no body was wanted** (`-I`,
   `data->req.no_body`), with any code but `0x10b`, is ignored: the head is written and
   the transfer ends with exit 0. curl only traces it, so nothing is printed.
4. **Every other reset** fails with
   `HTTP/3 stream <id> reset by server (error 0x<hex> <name>)`: exit 95 (`CURLE_HTTP3`)
   before any body byte and 18 (`CURLE_PARTIAL_FILE`) once body bytes have arrived.
   `<hex>` is the code in lower-case hex with no padding (`PRIx64`, so `0x10c`), and
   `<name>` is `vquic_h3_err_str`'s:

   | Code | Name |
   | --- | --- |
   | `0x100` | `NO_ERROR` |
   | `0x101` | `GENERAL_PROTOCOL_ERROR` |
   | `0x102` | `INTERNAL_ERROR` |
   | `0x103` | `STREAM_CREATION_ERROR` |
   | `0x104` | `CLOSED_CRITICAL_STREAM` |
   | `0x105` | `FRAME_UNEXPECTED` |
   | `0x106` | `FRAME_ERROR` |
   | `0x107` | `EXCESSIVE_LOAD` |
   | `0x108` | `ID_ERROR` |
   | `0x109` | `SETTINGS_ERROR` |
   | `0x10a` | `MISSING_SETTINGS` |
   | `0x10b` | `REQUEST_REJECTED` |
   | `0x10c` | `REQUEST_CANCELLED` |
   | `0x10d` | `REQUEST_INCOMPLETE` |
   | `0x10e` | `MESSAGE_ERROR` |
   | `0x10f` | `CONNECT_ERROR` |
   | `0x110` | `VERSION_FALLBACK` |
   | `0x21 + 0x1f × N`, the reserved greasing codes (RFC 9114 section 8.1) | `NO_ERROR` |
   | anything else | `unknown` |

   So ADR-0172's head without a valid `:status`, which the client aborts with
   `H3_MESSAGE_ERROR`, reads `HTTP/3 stream <id> reset by server (error 0x10e
   MESSAGE_ERROR)`, exit 95.

### What else moves from 8.18.0 to 8.21.0

Every other `failf` text in `curl_ngtcp2.c` at `curl-8_18_0` appears unchanged in
`cf-ngtcp2.c` and `cf-ngtcp2-cmn.c` at `curl-8_21_0`, and ADR-0177's reading of the QUIC
end of life (ingress errors, `check_and_set_expiry`, keep-alive, shutdown) holds there
too. The measured rows of ADR-0144 section 7 keep their 8.18.0 measurements: 8.21.0's
source prints the same text for each. These rows change:

| Row | 8.18.0 | 8.21.0 | Follow-up |
| --- | --- | --- | --- |
| ADR-0144 §7 and ADR-0172 §6: the server resets the request stream | `HTTP/3 stream <id> reset by server`, 95 or 18 | Decision 2 to 4 above | BL-834 |
| ADR-0172 §6: a head without a valid `:status` | `HTTP/3 stream <id> reset by server`, 95 | `… reset by server (error 0x10e MESSAGE_ERROR)`, 95 | BL-834, as a reset with `0x10e` |
| A new request stream cannot be opened (`ngtcp2_conn_open_bidi_stream` fails), no row yet | `can get bidi streams`, exit 55 | `cannot open bidi streams`, exit 55 | its own task, filed by BL-839 |
| `--http3-only` with `--unix-socket`, no row yet | exit 96 with no `failf` of its own, so curl's generic text for 96 | `HTTP/3 cannot be used over UNIX domain sockets`, exit 96 | its own task, filed by BL-839 |
| ADR-0172 §5: HTTP/3 through a proxy | a SOCKS proxy fails with exit 3 `HTTP/3 is not supported over a SOCKS proxy`; a tunnelling HTTP proxy with exit 3 `HTTP/3 is not supported over an HTTP proxy` | the SOCKS refusal stays; the HTTP proxy refusal is gone (8.21.0 can tunnel HTTP/3 through a proxy where built with `USE_PROXY_HTTP3`) | BL-837 |

## Consequences

- HTTP/3 fails as the release Curl names in `-V` and `User-Agent`, so the whole binary
  speaks for one curl.
- A server that sheds load with `H3_REQUEST_REJECTED` gets the request again on a new
  connection, as curl 8.21.0's users see, instead of a hard exit 95.
- Every reset text now carries the error code and its name, which BL-731's tests pin
  without; BL-834 changes them.
- The texts are read from source, not measured; a future measurement against a failing
  HTTP/3 server, with a curl 8.21.0 HTTP/3 build, may correct them.
- A later move to a newer curl release is a new ADR that re-reads these functions at that
  tag, not an edit of this one.

## Alternatives considered

- **Keep `curl-8_18_0`, the build ADR-0144 measured.** It is the only HTTP/3 curl that has
  run here, but Curl already says it is 8.21.0 everywhere else, and following 8.18.0 would
  leave a refused request failing where the release Curl names retries it. Rejected.
- **Follow `curl-8_22_0`, curl.se's current Windows release.** Its reset handling is the
  same as 8.21.0's, but it would put HTTP/3 one release ahead of `-V`, the User-Agent and
  every other decision pinned to 8.21.0. Rejected; a move of the whole product to a newer
  release would carry HTTP/3 with it.
- **Retry a refused stream under `--retry` only.** Simpler to reason about, but curl's
  retry is its own and runs without `--retry`. Rejected.
