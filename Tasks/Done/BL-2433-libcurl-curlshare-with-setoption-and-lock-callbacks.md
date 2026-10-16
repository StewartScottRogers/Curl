---
id: BL-2433
title: Libcurl: CurlShare with SetOption and lock callbacks
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Share, Libcurl.UnitTests/Share, Libcurl/Options/Behaviour, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2433 — Libcurl: CurlShare with SetOption and lock callbacks

## Goal

Libcurl has `CurlShare` (`curl_share_init/_setopt/_cleanup`) with `SHARE`/`UNSHARE` of every `CurlLockData` kind and the `LOCKFUNC`/`UNLOCKFUNC` delegates, and `CURLOPT_SHARE` on a `CurlEasy` attaches it.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin share/unshare of each kind, the lock callback calls, and attach and detach.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decisions in ADR-0579. `CurlShare` (`Libcurl/Share`) wraps the engine's `IShareHandle`; `SetOption`
  checks kinds, options and value types itself (`BadOption`), passes the seam's lock delegates
  through as given, and keeps `USERDATA` as `UserData`; `Cleanup` marks the handle cleaned up only on
  the engine's `Ok`, after which every call answers `Invalid`.
- Touches widened to `Libcurl/Options/Behaviour` (no task in Doing on `origin/work/dark-factory`
  names it): the `CURLOPT_SHARE` row now stores a `CurlShare`'s engine handle, so the engine never
  sees a Libcurl wrapper, and a cleaned-up `CurlShare` detaches as `setopt.c` does. Also
  `Libcurl/CLAUDE.md`, which the acceptance criteria name.
- `ShareInit` is a `CurlLibrary` part in `Libcurl/Share`, like `MultiInit` in `Multi/Core`.
- The managed engine's `CreateShare` still throws `NotSupportedException`; that is engine work.
- Measured: Libcurl.UnitTests 548 passed; `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl has CurlShare (curl_share_init/_setopt/_cleanup) with share/unshare of every lock kind and the lock callbacks, and CURLOPT_SHARE attaches and detaches it
