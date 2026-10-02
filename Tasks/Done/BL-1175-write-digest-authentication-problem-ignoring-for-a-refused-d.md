---
id: BL-1175
title: Write Digest authentication problem, ignoring. for a refused Digest answer in the HTTP handler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1175 — Write Digest authentication problem, ignoring. for a refused Digest answer in the HTTP handler

## Goal

Under `-v`, when a request that sent a Digest answer gets a `401` (or a forward proxy's `407`) whose Digest challenge does not carry `stale=true`, `HttpProtocolHandler` writes `* Digest authentication problem, ignoring.` just before the challenge header, as curl 8.21.0 does.

## Context

Measured in BL-1148 (curl 8.21.0, mingw, Schannel): `curl -s -S -v --digest -u u:p http://127.0.0.1:<P>/` answered a `401` Digest `nonce="a"`, then a `401` Digest `nonce="b"` (no stale), writes `* Digest authentication problem, ignoring.` between `< HTTP/1.1 401 Unauthorized` and `< WWW-Authenticate: Digest realm="r", nonce="b", qop="auth"`, and exits 0 with the 401. A stale challenge writes no such line. `HttpAuthProblemLines` writes the line only for Basic and Bearer today, and it splits challenges at commas, which would count each Digest parameter as a challenge; Digest needs its own reading (one line per Digest challenge, none when it is stale). Measure first with `Record-CurlExchange.ps1 -Connections 2`, including a header with two Digest challenges.

## Acceptance criteria

- [x] Measured and pinned in Notes with the curl version, for one and for two Digest challenges in the refusing `401`, and for a proxy `407`.
- [x] `HttpAuthProblemLinesTests` and an `HttpProtocolHandlerTests` case show the line written where curl writes it, and not for a stale challenge.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

Measured 2026-10-02, curl 8.21.0 (x86_64-w64-mingw32, Schannel), with
`Record-CurlExchange.ps1 -Connections 3`, first answer always a `401`
`WWW-Authenticate: Digest realm="r", nonce="a", qop="auth"` (no line before it):

- `-s -S -v --digest -u u:p`, second `401` `Digest realm="r", nonce="b", qop="auth"`:
  `* Digest authentication problem, ignoring.` just before that header, exit 0.
- Two Digest challenges in one header (`Digest realm="r", nonce="b", qop="auth", Digest realm="s", nonce="c"`):
  `* Digest authentication problem, ignoring.` then `* Ignoring duplicate digest auth header.`,
  both before the header. So "one line per Digest challenge" in Context was wrong: curl writes
  the problem line once, for the head's first Digest challenge (libcurl's `auth_digest`); a
  later Digest challenge in the same head writes the duplicate line instead.
- The two challenges in two headers: problem line before the first header, duplicate line
  before the second.
- `Basic realm="x", Digest realm="r", nonce="b"`: one Digest problem line, no Basic line.
- `stale=true`: no line; curl answers again and takes the `200`.
- No `-u` at all, one header with two Digest challenges: `* Ignoring duplicate digest auth header.`
  before it; with `-u u:p` (Basic) and two Digest headers: the duplicate line before the second.
  The duplicate line does not depend on what was sent.
- A first `401` whose Digest challenge lacks a nonce: no problem line (exit 94).
- Proxy: `-x http://127.0.0.1:P --proxy-digest -U u:p http://example.invalid/`, `407`
  `Proxy-Authenticate: Digest realm="r", nonce="a"` then `Proxy-Authenticate: Digest realm="r", nonce="b", Digest realm="s", nonce="c"`:
  the problem and the duplicate lines before the second `Proxy-Authenticate`.

Decisions: `HttpAuthProblemLines` became one instance per response head (the duplicate count
spans the head's headers), walking each header's comma-split challenges in order and writing
the Basic/Bearer line, the Digest problem line or the duplicate line as curl does; the handler
keeps one instance for the origin and one for the proxy per exchange. The duplicate line was
taken in because the same walk produces it and leaving it out would make the two-challenge
output differ from curl's. Staleness is read from the Digest challenge to the end of the
header. Known gap: a proxy `407` with two Digest challenges when no proxy credentials were
given writes no duplicate line here (the handler has no proxy auth request then); curl would.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes Digest authentication problem, ignoring. and Ignoring duplicate digest auth header. where curl 8.21.0 does, for a 401 and a proxy 407
