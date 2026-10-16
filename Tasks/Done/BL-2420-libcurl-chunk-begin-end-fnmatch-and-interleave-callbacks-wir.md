---
id: BL-2420
title: Libcurl: chunk begin/end, fnmatch and interleave callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2419, BL-2477, BL-2478]
touches: [Libcurl/Callbacks/Wildcard, Libcurl.UnitTests/Callbacks/Wildcard, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2420 — Libcurl: chunk begin/end, fnmatch and interleave callbacks wired through the engine

## Goal

CURLOPT_CHUNK_BGN_FUNCTION, CHUNK_END_FUNCTION, FNMATCH_FUNCTION (with `WILDCARDMATCH`) and INTERLEAVEFUNCTION are called by the engine with `CurlFileInfo`, as upstream's FTP wildcard and RTSP paths do.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin the wildcard download loop and the interleave call.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- 2026-10-16 (lane 1, prerequisite pass before any code): the engine has nothing to wire these
  callbacks to. No FTP code parses a `LIST` reply, matches a wildcard or loops over matched files,
  and nothing in `Curl.Protocol.Rtsp.UnitLibrary` hands interleaved `$` RTP frames anywhere
  (`RtspReplyReader` only skips them, BL-1663). The seam (`ChunkBeginCallback`, `ChunkEndCallback`,
  `FileNameMatchCallback`, `InterleaveCallback`, `CurlFileInfo`, `EasyBehaviourOptions.WildcardMatch`)
  already exists in `Curl.Api.UnitLibrary`. Filed BL-2476 (LIST parser and `curl_fnmatch`),
  BL-2478 (FTP wildcard download loop with begin, end and match hooks; depends on BL-2476) and
  BL-2477 (RTSP interleave sink), and requeued on BL-2477 and BL-2478. Once they are Done, this
  task adapts the four delegates to those hooks in `EngineComposition` (mapping the FTP library's
  file entries to `CurlFileInfo`, gated on `WildcardMatch`) and adds the `Libcurl/Callbacks/Wildcard`
  setters with their `*DATA` options taking any value, as the other callback areas do.
- 2026-10-16 (lane 2, done): `CallbackWildcardHooks` (an `IFtpWildcardHooks` over the three callbacks, each unset
  one falling back to `FtpDownloadEveryMatch.Instance`; `Of` is `null` without `WildcardMatch`) and
  `CallbackInterleaveSink` (an `IRtspInterleaveSink` over `Interleave`) are passed by `ManagedCurlEngine.CreateDispatcher`
  through two new optional `EngineComposition.CreateProtocolHandlers` parameters. Defaults taken: a chunk-begin answer
  other than Skip or Fail downloads (as `wc_statemach`'s switch); `CurlFileInfo.Time`, `UserId` and `GroupId` stay
  `null` (`ftplistparser.c` never fills them); `Curl.Console` passes neither hook. ADR-0619. Engine tests pin the
  wildcard loop (match, begin with `CurlFileInfo` and remains, end, skip, fail = exit 88, off = plain path) and the
  interleave call (frame handed whole, short take = 23) over a new `PerConnectionScriptedConnector`.
  `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary`: 0 failing members. The plan document
  (`Documentation/Planning/Curl-Api-And-libcurl.md`) is outside `touches`; ADR-0619 records the seam as built.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Backlog. Waits on BL-2477 (RTSP interleave sink) and BL-2478 (FTP wildcard loop, after BL-2476): the engine has nothing to wire the callbacks to yet
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Libcurl's chunk begin/end, fnmatch (under WILDCARDMATCH) and interleave callbacks run in the engine's FTP wildcard loop and RTSP reads
