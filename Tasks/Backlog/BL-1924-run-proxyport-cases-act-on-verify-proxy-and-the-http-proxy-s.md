---
id: BL-1924
title: Run %PROXYPORT cases: act on verify proxy and the http-proxy server in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1924 — Run %PROXYPORT cases: act on verify proxy and the http-proxy server in the case runner

## Goal

The case runner verifies `<verify><proxy>` and lets `<server>http-proxy</server>` cases run, so the %PROXYPORT cases that BL-1897 unblocked are measured instead of skipped.

## Context

Split out of BL-1897 (measured 2026-10-09, lane 2). BL-1897 gives `%PROXYPORT` a value (`UpstreamCaseRunner.ProxyPort`, 8992, reaching the same `sws` stand-in), but a conformance run shows every one of the 45 vendored cases using `%PROXYPORT` still skipped for another reason:

- 14 for "the harness does not act on <verify><proxy>": test80, 83, 95, 275, 744, 1078, 1184, 1287, 1288, 1297, 1428, 1904, 2050, 3028.
- 2 for "the harness does not emulate the http-proxy server": test445, test2107.
- The rest for `<client><tool>`, `<client><setenv>`, or another port (%FTPPORT, %HTTPSPORT, %SOCKSPORT, ...), which are other tasks.

Upstream runs a second `sws` with `--proxy` on %PROXYPORT and logs what it receives to its own protocol log, which `<verify><proxy>` compares (after `<strip>`) the way `<verify><protocol>` compares the HTTP server's. Starting point: have `SwsHttpServerConnector` record the bytes of connections made to `ProxyPort` (the `ConnectTarget` port) apart from the others, add them to `UpstreamCaseRun`, compare them in `UpstreamCaseVerification`, and drop `<verify><proxy>` from the screening's unsupported list; add `http-proxy` to the servers screening lets run. For a `-p` tunnel upstream's proxy log holds only the CONNECT, and the tunnelled request lands in the HTTP server's log - the tunnel itself is BL-1915, so a tunnel case may stay a real difference until then. Read Curl.Conformance.UnitLibrary\CLAUDE.md first; reference is curl 8.21.0 (`tests/runtests.pl`, `tests/server/sws.c`), read from the tarball into a scratch folder outside the repository.

## Acceptance criteria

- [ ] At least 15 named %PROXYPORT cases (the 16 above, less any that need the tunnel of BL-1915) run through UpstreamCaseRunner and get Passed or a real Curl difference; a test in Curl.Conformance.UnitTests pins that `<verify><proxy>` is compared, not skipped.
- [ ] Cases that now pass are added to PassingUpstreamCases.txt.
- [ ] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; no TestCategory=Integration.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for `<verify><proxy>` and `http-proxy`.

## Notes

## Log

- 2026-10-09: Created.
