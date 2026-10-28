# ADR-0819: The HTTP/2 options keep running on Windows under ADR-0871; CGR-040 is not fixed by refusing them

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3569); confirmed by Stewart on 2026-10-26 ("Keep HTTP/2 on"), so ADR-0871 stands
- Relates to: [ADR-0871](ADR-0871-windows-build-runs-http2.md), [ADR-0808](ADR-0808-windows-features-line-omits-http2-and-httpsrr.md)

## Context

Third-party gap report finding CGR-040 asks Curl on Windows to refuse `--http2`, `--http2-prior-knowledge`
and `--proxy-http2` with exit 2 (`the installed libcurl version does not support this`), as the Schannel
reference build does. Stewart decided the opposite in ADR-0871 (2026-10-22, "do it"): the three options run
on Windows as an nghttp2-built curl runs them. ADR-0808 later took `HTTP2` off the Windows `-V` Features line,
so a script probing `-V` sees what the reference shows, while the options still run.

Measured on 2026-10-26 with `Record-CurlExchange.ps1` against a loopback server answering
`HTTP/1.1 200 OK` with `hello\n` (and, for UC-16, `HTTP/1.1 101 Switching!\r\n\r\n` before it):

| Command | Schannel 8.22.0 (REF) | curl.se nghttp2 8.18.0 | Curl |
| --- | --- | --- | --- |
| `--http2 -s URL` | exit 2, refusal + try-help | exit 0, `hello`, h2c upgrade headers | exit 0, `hello`, the same request bytes |
| `--http2-prior-knowledge -s URL` | exit 2, refusal + try-help | exit 55, no output | exit 55, no output |
| `--proxy-http2 -s URL` | exit 2, refusal + try-help | exit 0, `hello`, plain HTTP/1.1 request | exit 0, `hello`, the same request bytes |
| `-s -S --http2 URL` (101 then 200) | exit 2, refusal | exit 16, `Remote peer returned unexpected data while we expected SETTINGS frame. ...` | exit 56, `Recv failure: Connection was aborted` |

## Decision

1. Curl keeps ADR-0871: on Windows the three options run as the nghttp2 build runs them. Refusing them would
   undo a decision Stewart made himself, which this delegation does not cover; CGR-040 is recorded as a
   known difference from the Schannel build, the one ADR-0871 names as its trade-off.
2. Where Curl differs from the nghttp2 build (the 101-then-HTTP/1 answer, exit 56 against 16), the fix
   belongs to BL-3650 (CGR-121), which covers HTTP/2 against a peer that does not speak it.

## Consequences

- CGR-040's acceptance criterion (match REF's exit 2) is not met; BL-3569 waits on Stewart to either confirm
  this ADR or reverse ADR-0871, in which case the three rows become `RefusedBySchannelBuild()` again.
