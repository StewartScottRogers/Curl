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
completed:
---
# BL-863 — Report curl's -v lines for a CONNECT 407 retry

## Goal

When `TcpConnector` retries a CONNECT after a `407`, it reports on `ConnectTarget.Events` the same `-v` lines, in the same order, that curl 8.21.0 prints.

## Context

BL-602 (see `Tasks/Doing/BL-602-answer-a-connect-tunnel-s-407-with-the-proxy-auth-scheme-cho.md`, Notes, or its archived copy once done) and `Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md` made `TcpConnector` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) answer a CONNECT `407` through `HttpProxyTunnelOptions.ProxyAuthenticator`. The measurements in BL-602's Notes were taken with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` acting as the proxy; follow the same method.

BL-602 left the retry's verbose lines unreported. curl 8.21.0 `-v` prints `Proxy auth using Digest with user 'u'` (or Basic) before a CONNECT carrying `Proxy-Authorization`, `Establishing HTTP proxy tunnel to example.test:80`, the CONNECT request and reply header lines, `Connect me again please` before dialling again after a `407` with `Connection: close`, and `Digest authentication problem, ignoring.` (or `Basic authentication problem, ignoring.`) when a `407` answers a CONNECT that already sent a credential. Check how `TcpConnector` already reports tunnel lines on `ConnectTarget.Events` and follow that. Tests use `Curl.Networking.UnitTests/Fakes/RecordingTransferEvents` and go beside the BL-602 cases in `TcpConnectorTests.ProxyAuth.cs`.

## Acceptance criteria

- [ ] The `-v` stderr of each BL-602 case (`--proxy-basic` vs 407 Basic; `--proxy-digest` 407 Digest then 200 with `Connection: close`; the same without `Connection: close`; `--proxy-anyauth` vs Basic and vs Digest; `--proxy-digest` 407 twice; `--proxy-anyauth` vs Basic twice) is measured with `Record-CurlExchange.ps1` and the lines this task covers are pinned in this task's Notes with the curl version.
- [ ] `TcpConnector` reports `Proxy auth using <Scheme> with user '<user>'` before each CONNECT that carries `Proxy-Authorization`, `Establishing HTTP proxy tunnel to <host>:<port>` before each CONNECT, `Connect me again please` before a redial, and `<Scheme> authentication problem, ignoring.` after a `407` to a CONNECT that sent a credential, each in curl's measured order and text; a `RecordingTransferEvents` test per case shows it.
- [ ] The CONNECT request and reply header lines are reported for both the first and the retried CONNECT, matching the measured output.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green across the solution; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
