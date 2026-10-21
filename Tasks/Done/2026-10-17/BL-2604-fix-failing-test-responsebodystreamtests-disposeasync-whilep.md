---
id: BL-2604
title: Fix failing test ResponseBodyStreamTests.DisposeAsync_WhilePaused_AbortsTheTransfer (PerformResult not WriteError)
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api, Curl.Api.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2604 — Fix failing test ResponseBodyStreamTests.DisposeAsync_WhilePaused_AbortsTheTransfer (PerformResult not WriteError)

## Goal

`Curl.Api.UnitTests`' `ResponseBodyStreamTests.DisposeAsync_WhilePaused_AbortsTheTransfer` passes on every run.

## Context

Found by BL-2585's lane on 2026-10-17 at commit 6cd5ed0bb: the test fails alone and in the fast run, `Assert.AreEqual(CurlCode.WriteError, easy.PerformResult)` failing. It drives the fake `SteppedEasyTransfer` only, so BL-2585's engine change does not reach it. Start in `Curl.Api/ResponseBodies/ResponseBodyStream.cs` and the fake's pause and resume.

## Acceptance criteria

- [x] `dotnet test Curl.Api.UnitTests --filter "Name~DisposeAsync_WhilePaused_AbortsTheTransfer"` passes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Cause: the fake waited for a resume with the perform token, so on dispose the token cancel raced the unpause: AbortedByCallback or WriteError. A real engine does not watch the token while paused; the fake now waits on the resume alone, so the aborted buffer answers the error and the result is WriteError every time.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 44cec74f 3d836d5e.
- 2026-10-17: Doing -> Done. Fake no longer races the perform token against resume; test deterministic, fast tests green
