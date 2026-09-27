---
id: BL-299
title: Make -m span the whole -L redirect chain, not each hop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-174]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-299 — Make -m span the whole -L redirect chain, not each hop

## Goal

`curl -L -m 2` ends a redirect chain with exit 28 once 2 seconds have passed since the first request, however many hops it has taken.

## Context

- Found in BL-174. `HttpTransferDeadline` (Curl.Protocol.Http.UnitLibrary) starts the `-m` clock when `HttpProtocolHandler.ExecuteAsync` is called, and `RedirectFollower` (Curl.Core.UnitLibrary) calls the handler once per hop, passing `MaxTime` unchanged (RedirectFollower.cs, the context copy), so each hop gets a fresh `-m`. curl's `-m` limits the whole operation (https://curl.se/docs/manpage.html#-m).
- ADR-0040 records the per-handler design and names this gap.
- Likely approach: the follower passes each hop the time left (`MaxTime` minus the elapsed time on `TimeProvider`), and a hop started with none left fails with exit 28. Measure curl 8.21.0 (`/mingw64/bin/curl`) against a loopback redirect chain whose second hop stalls, and pin the N and M it prints.

## Acceptance criteria

- [ ] A `RedirectFollowerTests` test on `FakeTimeProvider`: with `MaxTime` 2 s, a first hop that takes 1.5 s and a second hop that stalls ends with exit 28 at 2 s total, with the measured message.
- [ ] The measured curl 8.21.0 command and output are recorded in Notes.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the libraries touched.

## Notes

## Log

- 2026-09-26: Created.
