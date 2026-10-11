---
id: BL-2083
title: Cover QuicConnection.ReceiveOrTimeOutAsync's wake path deterministically
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2083 — Cover QuicConnection.ReceiveOrTimeOutAsync's wake path deterministically

## Goal

A fast unit test drives `QuicConnection.ReceiveOrTimeOutAsync`'s wake-token catch (the
`OperationCanceledException` raised because a stream change woke the loop, not the timer)
on every run, so the CI Quality gates job no longer fails at random on that member.

## Context

CI's Quality gates job (BL-2068) failed on gap PR #117 (run 38159879622), a PR that changes
only `Gap/` files: `QuicConnection.ReceiveOrTimeOutAsync (async)` at
`Curl.Quic.UnitLibrary\QuicConnection.cs:247` measured 90.91% lines, missing lines 271
and 273. The same code passed on master (run 38158290319). So the existing tests reach the
wake path only when the scheduling happens to fall that way. Line coverage that depends on
timing is a flaky gate.

Start at `Curl.Quic.UnitLibrary/QuicConnection.cs` around line 246 and the QuicConnection
tests in `Curl.Quic.UnitTests`. Use the injected `TimeProvider` and a fake datagram channel
whose `ReceiveAsync` waits until its token is cancelled. Then signal a stream change, so the
wake token (not the timer and not stop) cancels the receive. Never use `Thread.Sleep`.

## Acceptance criteria

- [ ] A test in `Curl.Quic.UnitTests` deterministically reaches the wake-token catch of `ReceiveOrTimeOutAsync` and asserts the loop goes on to send what the stream change queued.
- [ ] Run 20 times, `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports `ReceiveOrTimeOutAsync` at 100% line and branch coverage every time.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created from Quality gates run 38159879622 (gap PR #117).
- 2026-10-11: Backlog -> Doing.
