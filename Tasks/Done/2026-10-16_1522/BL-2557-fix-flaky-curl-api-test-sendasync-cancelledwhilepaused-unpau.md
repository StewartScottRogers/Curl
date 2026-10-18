---
id: BL-2557
title: Fix flaky Curl.Api test SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle (ProgressCalls 0)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2557 — Fix flaky Curl.Api test SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle (ProgressCalls 0)

## Goal

`CurlClientCancellationTests.SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle` passes every run instead of sometimes failing with `ProgressCalls` 0.

## Context

`Curl.Api.UnitTests/Cancellation/CurlClientCancellationTests.cs`; ADR-0639 (cancel through the transfer-info callback), ADR-0644 (the client's multi handle driver).

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The task was filed with the template's placeholder criterion; the board refuses a reworded criterion, so it is ticked as filed. What it stands for, checked: the named test no longer depends on thread timing, `dotnet build` is clean and the fast tests are green (Curl.Api.UnitTests 895 passed, every project passed).
- Cause: on cancellation two registrations on the same token race. `TransferCancellation` unpauses the handle so the transfer can ask its transfer-info callback and abort, and `MultiTransferDriver.RunAsync` removes the transfer from the multi handle and finishes it with `AbortedByCallback` (ADR-0644). `EasyPerformingMultiTransfer.Remove` does not wait for the easy perform, so when the removal wins, the handle is freed before the fake transfer asks progress once, and `ProgressCalls` stays 0. Both outcomes are correct product behaviour (libcurl's `curl_multi_remove_handle` also stops a transfer without a progress call), so the fix is in the test only.
- Fix: the test now asks the transfer-info callback directly after cancelling and asserts it answers `TransferCancellation.Abort`, which holds whichever path ends the transfer; `ResumeCalls == 1` and `Disposed` stay (both settled before `CancelAsync` and `DisposeAsync` return). No production code changed, so no quality measurement was needed.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-16: Doing -> Done. SendAsync_CancelledWhilePaused_UnpausesAbortsAndFreesTheHandle no longer races the multi driver's removal: it checks the transfer-info callback answers abort
