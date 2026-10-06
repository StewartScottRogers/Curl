---
id: BL-863
title: Report curl's -v lines for a CONNECT 407 retry
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-602]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-863 — Report curl's -v lines for a CONNECT 407 retry

## Goal

When `TcpConnector` retries a CONNECT after a `407`, it reports on `ConnectTarget.Events` the same `-v` lines, in the same order, that curl 8.21.0 prints.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

BL-602 left the retry's verbose lines unreported. curl 8.21.0 `-v` prints `Proxy auth using Digest with user 'u'` (or Basic) before a CONNECT carrying `Proxy-Authorization`, `Establishing HTTP proxy tunnel to example.test:80`, the CONNECT request and reply header lines, `Connect me again please` before dialling again after a `407` with `Connection: close`, and `Digest authentication problem, ignoring.` (or `Basic authentication problem, ignoring.`) when a `407` answers a CONNECT that already sent a credential. Check how `TcpConnector` already reports tunnel lines on `ConnectTarget.Events` and follow that. Tests use `Curl.Networking.UnitTests/Fakes/RecordingTransferEvents` and go beside the BL-602 cases in `TcpConnectorTests.ProxyAuth.cs`.

## Acceptance criteria

- [x] The `-v` stderr of each BL-602 case (`--proxy-basic` vs 407 Basic; `--proxy-digest` 407 Digest then 200 with `Connection: close`; the same without `Connection: close`; `--proxy-anyauth` vs Basic and vs Digest; `--proxy-digest` 407 twice; `--proxy-anyauth` vs Basic twice) is measured with `Record-CurlExchange.ps1` and the lines this task covers are pinned in this task's Notes with the curl version.
- [x] `TcpConnector` reports `Proxy auth using <Scheme> with user '<user>'` before each CONNECT that carries `Proxy-Authorization`, `Establishing HTTP proxy tunnel to <host>:<port>` before each CONNECT, `Connect me again please` before a redial, and `<Scheme> authentication problem, ignoring.` after a `407` to a CONNECT that sent a credential, each in curl's measured order and text; a `RecordingTransferEvents` test per case shows it.
- [x] The CONNECT request and reply header lines are reported for both the first and the retried CONNECT, matching the measured output.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured curl 8.21.0 (x86_64-w64-mingw32, Schannel) 2026-10-01 with `Record-CurlExchange.ps1 -Port 18863 -Connections 3` as the proxy, `-v -s -S -p -x http://127.0.0.1:18863 -U u:p <switch> http://example.test/`, the 407s as in BL-602's Notes (with `Connection: close`). Lines this task covers, in order (`>`/`<` lines abridged to their roles):
  - `--proxy-basic` vs 407 Basic: `Trying`, `CONNECT: no ALPN negotiated`, `Proxy auth using Basic with user 'u'`, `Establishing HTTP proxy tunnel to example.test:80`, `> CONNECT ...` head with `Proxy-Authorization: Basic dTpw`, `< HTTP/1.1 407 ...`, `< Proxy-Authenticate: Basic realm="r"`, **`Basic authentication problem, ignoring.`** (after the header line, not before as for an origin 401), `< Content-Length: 0`, `< Connection: close`, `< `, then `CONNECT tunnel failed, response 407`, exit 7.
  - `--proxy-digest`, 407 Digest then 200: `Proxy auth using Digest with user 'u'` before **both** CONNECTs (the first sends no value), the first head without `Proxy-Authorization`, the 407's lines, `Connect me again please`, `Trying`, `CONNECT: no ALPN negotiated`, `Proxy auth using Digest ...`, `Establishing ...`, the Digest head, `< HTTP/1.1 200 Connection established`, `< `, then `CONNECT phase completed for HTTP proxy`, `CONNECT tunnel established, response 200`.
  - The same without `Connection: close` (`-Script`, `Content-Length: 6` body `denied`): no `Connect me again please`, no second `Trying`; after `< ` comes `Ignore 6 bytes of response-body`, then `Proxy auth using Digest ...`, `Establishing ...` and the second head on the same connection.
  - `--proxy-anyauth` vs Basic / vs Digest: no `Proxy auth using` before the first CONNECT; after `Connect me again please` the second CONNECT has `Proxy auth using Basic` (or `Digest`) `with user 'u'`.
  - `--proxy-digest` 407 twice: as above, then the second 407's `< Proxy-Authenticate: Digest ...` is followed by `Digest authentication problem, ignoring.`, exit 7. `--proxy-anyauth` vs Basic twice: the same with `Basic authentication problem, ignoring.`.
- Implementation: `ConnectTunnelVerboseLines` (new, internal) formats the lines; `TcpConnector.RequestTunnelAsync` reports `Proxy auth using`/`Establishing`, the request head as one `ReportRequestHeader` and each reply line as `ReportResponseHeader`, and `ConnectThroughProxyAsync` reports `Connect me again please` before a redial. The scheme rule follows `HttpAuthUsingLines` (Digest before any value for `--proxy-digest` alone with a user; else Basic, Digest or NTLM from the value); Negotiate is left unnamed (not measured for CONNECT). Problem lines follow `HttpAuthProblemLines`' challenge match, for Basic and Digest values.
- Choice: the `Establishing HTTP proxy tunnel to` text is the Schannel 8.21.0 one on every platform; BL-872 saw the OpenSSL 8.18.0 build print `Establish HTTP proxy tunnel to`, which BL-964 is to measure on 8.21.0 and split per build with its other CONNECT-phase lines. No ADR: the behaviour is measured output, not a design choice.
- Out of scope, filed: BL-1145 (`CONNECT: no ALPN negotiated` before a plain HTTP proxy's CONNECT) and BL-1146 (`Ignore N bytes of response-body` and the chunked lines). BL-964 already covers `CONNECT phase completed`/`CONNECT tunnel established`.
- `RecordingTransferEvents` gained `Transcript` (info, request and response lines as `-v` shows them); the two existing tests pinning `events.Info` through a proxy now expect `Establishing HTTP proxy tunnel to ...` at the end.
- Gates: `dotnet build -warnaserror` clean; fast tests green across the solution (Curl.Networking.UnitTests 2436 passed, 25 skipped; Curl.Console.UnitTests 2070 passed); `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100/100, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. TcpConnector reports curl's -v lines for each CONNECT and its 407 retry
