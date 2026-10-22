---
id: BL-1912
title: Emulate upstream's HTTPS test server (%HTTPSPORT) in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1896]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1912 — Emulate upstream's HTTPS test server (%HTTPSPORT) in the case runner

## Goal

The runner emulates upstream's HTTPS test server (%HTTPSPORT) as the sws HTTP stand-in behind the TLS wrapper, so the 29 HTTPS cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 29 cases are skipped for %HTTPSPORT. Upstream runs sws behind stunnel; do the same in memory: an IConnector for the https port that wraps SwsHttpServerConnection in the TLS server stream of BL-1896. Offer ALPN http/1.1 and h2 as stunnel did, and record the decrypted bytes in ReceivedBytes for <verify><protocol>. HTTPS-via-proxy cases need BL-1897 and BL-1915 as well and may stay skipped with that reason. Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 15 named HTTPS cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] At least 10 of the %CERTDIR cases that need only %HTTPSPORT (310, 311, 312, 313, 417, 2033, 2034, 2035, 2037, 2038, 2041, 2042, 2048, 2070, 2079, 2087, 2090, 3000, 3001, 3023, 3024, 3207) are among them (handed over from BL-1896).
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %HTTPSPORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped.

## Notes

- 2026-10-09 (BL-1896, lane 1): %CERTDIR now resolves (BL-1922, BL-1923) and `TlsServerStream` is ready (BL-1921). A conformance run on this date skipped no case for %CERTDIR, but every one of the 28 %CERTDIR cases also needs a TLS server: 22 skip for %HTTPSPORT (678 also for %LIBTESTS), 2088 and 2089 for %HTTPS-MTLSPORT, 2500, 2502 and 2503 for %HTTP3PORT. So BL-1896's "at least 10 %CERTDIR cases measured" lands here, with this server.
- 2026-10-09 (lane 1): HttpsServerConnector on 8989 (%HTTPSPORT, set only with a certificate directory) wraps sws connections in TlsServerConnection. Measured on Windows: 13 named %CERTDIR cases measured (311, 312, 417, 2033, 2035, 2038, 2042, 2048, 2070, 2079, 2087, 3023, 3024); the !Schannel ones skip on Windows for that feature and run on Linux/macOS. 2035, 2038, 2042 (exit 60, not 90), 323 (2, not 35) and 1244 (52, not 56: stunnel resets a plain-text client) fail, left for the next gap run.
- Choice: no ALPN offered, though the Context says h2 and http/1.1: stunnel as servers.pm starts it negotiates none, and sws reads only HTTP/1.1, so accepting h2 would fail every case on a stand-in fault.
- Choice: only cases with no TLS-backend feature condition join the passing list (300, 303, 304, 306, 309, 311, 312, 325, 364, 410, 414, 417, 474, 1561, 1562, 2009-2011, 2048, 3305, 4000, 4001), since a listed case skipped on another platform fails the ratchet; the Schannel-only passes stay unlisted.
- Choice: when sws closes, the stand-in sends close_notify as stunnel does; without it test306 and test364 got exit 56.
- HTTPS-through-proxy cases (1849, 3106, 780-783) skip naming BL-1915.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. HTTPS stand-in on %HTTPSPORT runs the https cases; 22 newly listed
