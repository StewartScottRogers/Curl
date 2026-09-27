---
id: BL-310
title: Carry the transfer progress sink across redirect hops in RedirectFollower
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-134]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-310 — Carry the transfer progress sink across redirect hops in RedirectFollower

## Goal

Every hop `RedirectFollower` builds carries the first hop's `ITransferContext.Progress`, so a `-L` transfer reports "started" and byte counts to the same sink as a transfer without redirects.

## Context

ADR-0045 gives `ITransferContext` a `Progress` member (`ITransferProgress`) that `TransferContext` defaults to `NoTransferProgress.Instance`. `Curl.Core.UnitLibrary/RedirectFollower.cs` builds each hop with `NextHop`, which copies every context member by hand into a new `TransferContext`. Because the new member has a default, the compiler does not flag `NextHop` for leaving it out, and a followed redirect would then report to the do-nothing sink. BL-134 adds the member; this task copies it.

## Acceptance criteria

- [x] `RedirectFollower.NextHop` sets `Progress = first.Progress`.
- [x] A test in `Curl.Core.UnitTests/RedirectFollowerTests.cs` follows one redirect with a context whose `Progress` is a recording sink and pins that the second hop's context returns that same sink instance.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; 100% line and branch coverage of `Curl.Core.UnitLibrary` holds.

## Notes

- Pipeline shortened (2026-09-27, lane 1): the change is one line in `NextHop` plus a test, fully specified by the acceptance criteria, so it was made directly without a separate architect plan; no design question arose, so no ADR.
- Test: `RedirectFollowerTests.FollowAsync_NextHop_ReportsToTheFirstHopsProgressSink` follows one 302 with a private `RecordingTransferProgress` and pins `AreSame` on the second hop's `Progress`. A separate test rather than a line in `FollowAsync_NextHop_CarriesEveryOtherOptionUnchanged`, so a failure names the missing member.
- Verified: `dotnet build` 0 errors; fast tests all green (Curl.Core.UnitTests 801 passed, 3 skipped); Curl.Core.UnitLibrary line-rate 1, branch-rate 1 (cobertura).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Followed -L redirect hops report to the first hop's progress sink
