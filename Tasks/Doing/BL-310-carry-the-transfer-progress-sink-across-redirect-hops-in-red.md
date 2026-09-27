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
completed:
---
# BL-310 — Carry the transfer progress sink across redirect hops in RedirectFollower

## Goal

Every hop `RedirectFollower` builds carries the first hop's `ITransferContext.Progress`, so a `-L` transfer reports "started" and byte counts to the same sink as a transfer without redirects.

## Context

ADR-0045 gives `ITransferContext` a `Progress` member (`ITransferProgress`) that `TransferContext` defaults to `NoTransferProgress.Instance`. `Curl.Core.UnitLibrary/RedirectFollower.cs` builds each hop with `NextHop`, which copies every context member by hand into a new `TransferContext`. Because the new member has a default, the compiler does not flag `NextHop` for leaving it out, and a followed redirect would then report to the do-nothing sink. BL-134 adds the member; this task copies it.

## Acceptance criteria

- [ ] `RedirectFollower.NextHop` sets `Progress = first.Progress`.
- [ ] A test in `Curl.Core.UnitTests/RedirectFollowerTests.cs` follows one redirect with a context whose `Progress` is a recording sink and pins that the second hop's context returns that same sink instance.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; 100% line and branch coverage of `Curl.Core.UnitLibrary` holds.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
