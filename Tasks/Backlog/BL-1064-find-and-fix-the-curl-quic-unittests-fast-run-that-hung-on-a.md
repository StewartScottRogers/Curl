---
id: BL-1064
title: Find and fix the Curl.Quic.UnitTests fast run that hung on a second dotnet test
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1064 — Find and fix the Curl.Quic.UnitTests fast run that hung on a second dotnet test

## Goal

`Curl.Quic.UnitTests` finishes every fast run: no test can wait forever, so `dotnet test --filter "TestCategory!=Integration"` never hangs in it.

## Context

- Seen 2026-09-30 on dark factory lane 1 (BL-886, a comment-only change): the first fast run of `Curl.slnx --no-build` passed `Curl.Quic.UnitTests` (405 of 405, 804 ms); an immediate second run of the same build hung in `Curl.Quic.UnitTests`'s `testhost.exe` for the whole 3600 s tool timeout and was killed. Every other project had finished.
- Suspects: a test awaiting a `TaskCompletionSource`, channel read or fake `TimeProvider` timer that a race leaves unsignalled, or a loopback UDP socket that never receives. Start by running the project in a loop (`for ($i=0;$i -lt 50;$i++){ dotnet test Curl.Quic.UnitTests --no-build --filter "TestCategory!=Integration" --blame-hang-timeout 2m }`) and read the blame dump's hung test.

## Acceptance criteria

- [ ] The hanging test is named in Notes, with the cause.
- [ ] 50 consecutive `dotnet test Curl.Quic.UnitTests --filter "TestCategory!=Integration" --blame-hang-timeout 2m` runs all pass with no hang.
- [ ] Every awaited wait in `Curl.Quic.UnitTests` that can hang is bounded (a `[Timeout]` or a cancellation token), so a regression fails instead of hanging.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-30: Created.
