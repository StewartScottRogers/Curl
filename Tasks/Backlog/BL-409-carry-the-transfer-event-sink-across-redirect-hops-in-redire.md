---
id: BL-409
title: Carry the transfer event sink across redirect hops in RedirectFollower
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-409 — Carry the transfer event sink across redirect hops in RedirectFollower

## Goal

`RedirectFollower.NextHop` copies `Events` from the first context, so `-L -v` shows every hop.

## Context

- ADR-0046, "Who does what next": `Events` must be carried across redirect hops beside the `Progress` copy, because the default hides a missing copy from the compiler.
- Found in BL-242: `Curl.Core.UnitLibrary/RedirectFollower.cs` `NextHop` sets `Progress = first.Progress` but not `Events`, so every hop after the first reports to `NoTransferEvents.Instance`.

## Acceptance criteria

- [ ] A `RedirectFollowerTests` test shows the second hop's context carries the first context's `Events` instance.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Filed from BL-242 (2026-09-27), which wired `-v` and `--trace` in `Curl.Console`.

## Log

- 2026-09-27: Created.
