---
id: BL-2412
title: Libcurl: GetInfo for string and list CURLINFO values, and the dispatch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Info/Strings, Libcurl.UnitTests/Info/Strings, Libcurl/Info/CurlInfo.cs, Libcurl/Info/EasyInfoDispatch.cs, Libcurl/Info/EasyInfoReaders.cs, Libcurl/Info/EasyInfoReader.cs, Libcurl/Info/CurlTlsSessionInfo.cs, Libcurl.UnitTests/Info/EasyInfoDispatchTests.cs, Libcurl/Easy/CurlEasy.cs, Libcurl.UnitTests/Easy/FakeEasyTransfer.cs, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2412 — Libcurl: GetInfo for string and list CURLINFO values, and the dispatch

## Goal

Libcurl has `CurlInfo` (every `CURLINFO_*` by name and id) and `CurlEasy.GetInfo(CurlInfo)` dispatching by type; this task answers the strings (`EFFECTIVE_URL`, `REDIRECT_URL`, `CONTENT_TYPE`, `PRIMARY_IP`, `LOCAL_IP`, `SCHEME`, `REFERER`, `CAINFO`, `CAPATH`, `PRIVATE`, ...) and lists (`SSL_ENGINES`, `COOKIELIST`, `CERTINFO`), and `TLS_SSL_PTR` answers the Schannel build's shape, documented.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per info reads `TransferInfo` from a fake transfer; an unknown id returns `CurlCode.UnknownOption`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- The goal assumed `CurlInfo` and `GetInfo` already existed; they did not, so this task built them
  (ADR-0566): `CurlInfo` (every `CURLINFO_*` of 8.22.0's `curl.h`, `HttpCode` alias),
  `CurlEasy.GetInfo(CurlInfo, out object?)`, the internal `EasyInfoDispatch` (`UnknownOption` for an
  id `curl.h` lacks, `NotBuiltIn` for a known info no group reads yet) and `EasyInfoReaders` with
  `AddStringReaders`, `AddLongReaders` (BL-2413) and `AddTimeReaders` (BL-2414) partial methods.
- Touches widened for that (no other Doing task on `origin/work/dark-factory` names any of them):
  the dispatch, reader and `CurlTlsSessionInfo` files in `Libcurl/Info`, `CurlEasy.cs` for
  `GetInfo`, `FakeEasyTransfer.cs` (its `Info` now settable instead of throwing), the dispatch tests
  and `Libcurl/CLAUDE.md`.
- `TLS_SSL_PTR` and the deprecated `TLS_SESSION` answer `CurlTlsSessionInfo(backend, null)`: 8 on
  Windows (Schannel), 1 elsewhere; no native pointer. `PRIVATE` reads `EasyBehaviourOptions.Private`.
- First quality run failed `AddStringReaders` at complexity 36: the report counts each lambda in the
  adding method. Readers are now named static methods in explicit `new EasyInfoReader(...)` (an
  implicit static method group is cached behind a null check, a branch). Second run: Libcurl 100/100,
  0 failing, worst CRAP 6.
- Fast tests: all green; Libcurl.UnitTests 181. `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlEasy.GetInfo answers every string and list CURLINFO, PRIVATE and TLS_SSL_PTR through the new CurlInfo table and EasyInfoDispatch
