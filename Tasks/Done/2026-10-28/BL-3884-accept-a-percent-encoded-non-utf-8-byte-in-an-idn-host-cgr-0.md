---
id: BL-3884
title: Accept a percent-encoded non-UTF-8 byte in an IDN host (CGR-059 a%FFb)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlPunycode.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejection.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Protocol.Abstractions.UnitTests/Cgr059PercentEncodedHostTests.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlTests.cs, Curl.Protocol.Abstractions.UnitTests/Cgr016UrlTests.cs, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitTests/TransferPerformerTests.Cgr059.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3884 — Accept a percent-encoded non-UTF-8 byte in an IDN host (CGR-059 a%FFb)

## Goal

Curl transfers `http://a%FFb:20200/a` as curl 8.22.0 does on Windows.

## Context

Split from BL-3588 (CGR-059 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, Medium, url-parsing, related GF-0028). Suspected location: `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` (about lines 100-118 and 177-186; may have moved). Measure real curl first; the server needed answers `200 ok` and records the request (Record-CurlExchange.ps1 or a C# file-based server, no Python).

Repro: `curl -sS "http://a%FFb:20200/a" --connect-to ::127.0.0.1:20200`

Expected (real curl): exit 0, request has `Host: xn--ab-6ka`. Curl today: exit 3, `URL rejected: Bad hostname`.

## Acceptance criteria

- [x] The repro above run through real curl and through Curl gives the same exit code, stdout bytes, stderr text and request bytes on the wire; the command and both results are recorded in Notes.
- [x] A focused unit test in `Cgr059*Tests.cs` named in `touches` (in Curl.Protocol.Abstractions.UnitTests) fails before the fix and passes after it (ADR-0482).
- [x] Host cases that already matched curl still match (existing CurlUrlHost tests stay green).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3588 (over its cost cap).
- Continues the stashed work of BL-3588.
- Repro measured 2026-10-28 with Record-CurlExchange.ps1 -Port 20200 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 3\r\n\r\nok\n' -CurlArgs '-sS','http://a%FFb:20200/a','--connect-to','::127.0.0.1:20200': real curl 8.22.0 (Windows) and Curl both exit 0, stdout ok\n, empty stderr, request GET /a HTTP/1.1\r\nHost: xn--ab-6ka:20200\r\nUser-Agent: curl/8.22.0\r\nAccept: */*\r\n\r\n, byte for byte identical.
- Unit test: Cgr059PercentEncodedHostTests.TryNormalize_StrayBytesAsWindows1252_ReadsThemInThatCodePage (a%FFb is xn--ab-6ka); before the fix the host was refused as Bad hostname.
- Decision recorded in ADR-0942 (renumbered from the stash's ADR-0823, which another lane took for -X/-b argv bytes); removed two stale ADR-0822 README rows the stash carried for files that do not exist here.
- touches widened to the stash's other files (CurlUrlPunycode, CurlUrlRejection, CurlUrlRejectionMessages, CurlUrlTests, Cgr016UrlTests, Engine's TransferPerformer.Setup, ManagedEasyTransfer.Perform and TransferPerformerTests.Cgr059): the label-by-label conversion and its exit-3 text come with the fix, and no task in Doing names any of them.
- Cgr016UrlTests.TryParse_HostHoldingAnInvalidUtf8Byte_IsRefused was measured on Linux; Windows now reads the byte as Windows-1252 as the reference build does, so it is pinned off Windows.
- Linux and macOS: the Windows-1252 reading is behind OperatingSystem.IsWindows() and every new test passes the flag explicitly, so they are platform-neutral; fast tests green here.
- Measure-CodeQuality.ps1 for Curl.Protocol.Abstractions.UnitLibrary and Curl.Engine.UnitLibrary: 0 failing members.
- Filed BL-3945 (invalid xn-- label refusal) depending on this task.
- Where curl's answer differs by platform, pin each platform's answer in its own test (CLAUDE.md).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 12e52b50 1b26f5a2 3be9ea7b 779790c0.
- 2026-10-28: Doing -> Done. a%FFb transfers as xn--ab-6ka on Windows, byte-identical to curl 8.22.0
