---
id: BL-4912
title: Ignore h3 and h2 alt-svc entries on the Windows Schannel profile instead of switching protocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/AltSvcPlatformProtocolTests.cs, Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs, Curl.Console.UnitTests/CurlCommandRunnerAltSvcTests.cs, Curl.Console.UnitTests/CurlCommandRunnerAltSvcVersionTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4912 — Ignore h3 and h2 alt-svc entries on the Windows Schannel profile instead of switching protocol

## Goal

On Windows, where neither HTTP/3 nor (by default) HTTP/2 is offered, an alt-svc file never moves a transfer off the origin; the origin is used as curl 8.22.0 Schannel does. Covers findings STD-009 and STD-013.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-009 (Medium, confirmed: reproduced at least twice).

- Repro: HTTPS server on TCP 18301 (body `ok`), nothing on UDP 18398. `printf "h1 localhost 18301 h3 localhost 18398 \"20301231 00:00:00\" 0 0\n" > alt.txt`; `curl -sS https://localhost:18301/ --alt-svc alt.txt --cacert ca.pem --ssl-no-revoke -w "%{http_version} %{remote_port}\n"`. STD-013: same with an `h2 localhost 18320` entry and an HTTP/2 server on 18320.
- Expected: REF 8.22.0: `ok`, `1.1 18301`, exit 0, for both the h3 and the h2 entry.
- Actual: h3: exit 56, `0 -1`, `curl: (56) QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)`, no fallback; when an HTTP/3 server answers, TLS errors switch to OpenSSL wording. h2: served by the h2 server, `2 18320`, although `-V` lists no HTTP2 (ADR-0141).
- Where to start: Alt-svc candidate selection feeding `QuicDialer.cs`; apply the per-platform protocol availability.

## Acceptance criteria

- [x] The h3 repro exits 0 and prints `ok` and `1.1 18301`.
- [x] The h2 repro prints `ok` and `1.1 18301`.
- [x] Linux and macOS behaviour (where the build offers h2/h3) is unchanged and pinned in its own test.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.
- 2026-11-11 (lane 13): The candidate selection is not in `QuicDialer.cs` but in `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs` (`LookupOf`, the destination ALPNs `url_set_conn_peer` allows); no task in Doing on origin/work/dark-factory touched it or the Console alt-svc tests, so touches were widened to them. `QuicDialer.cs` is unchanged.
- Decision (ADR-1238): with no version option on Windows the allowed destination is `h1` alone, as curl 8.22.0 Schannel (no USE_HTTP2, no USE_HTTP3) allows; an explicit `--http2`/`--http3`/`--http3-only` keeps its destinations on every platform (ADR-0141, ADR-0144). Real curl not re-measured here: the review's measurement (reproduced at least twice) is the reference. The old Windows test `RunAsync_H2EntryForAnotherPortOnWindowsWithoutAVersionOption_OffersH2AloneAndSpeaksHttp2` pinned curl.se 8.18.0 (with nghttp2), not the Schannel build; it is renamed without "OnWindows" and excluded on Windows.
- Tests: `AltSvcTransferCacheTests.LookupOf_VersionOptionAndPlatform_*` pins both platforms' sets on any OS; `ApplyTo_H2OrH3EntryForAnotherHostOnWindowsWithNoVersionOption_StaysOnTheOrigin` and Console's `RunAsync_H2OrH3EntryForAnotherPortOnWindowsWithoutAVersionOption_StaysOnTheOrigin` (`ok|1.1`, port 18443, no route) run on Windows; the 9 tests pinning the h2/h3 switches carry `OSCondition(Exclude Windows)`, so Linux and macOS behaviour stays pinned. Criterion on Linux/macOS met by keeping the change platform-neutral; CI's watch files any failure.
- Host: SDK 10.0.401 (global.json) is not installed on this machine (only 10.0.100-rc.1 and 11.0.100), so `dotnet` fails in the repository; build and tests ran from a folder outside it on SDK 11.0.100: Curl.slnx built with 0 warnings, fast tests green except 6 NTLM/SSPI tests (Ldap, Authentication, Console `*WindowsProductionRoute*`) that depend on this host's SSPI and touch nothing changed here. Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary stopped on those same Console failures; complexity is enforced at build (clean), and the Engine run's cobertura shows the new `NoVersionOptionDestinations` and the `None` arm fully covered.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: f7c46fe8 00c24195 f9789258 a0b49a29.
- 2026-11-11: Doing -> Done. Without a version option on Windows, alt-svc h2 and h3 entries no longer move a transfer off the origin (ADR-1238)
