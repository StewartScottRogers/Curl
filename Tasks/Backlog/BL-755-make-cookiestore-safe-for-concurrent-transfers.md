---
id: BL-755
title: Make CookieStore safe for concurrent transfers
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-755 — Make CookieStore safe for concurrent transfers

## Goal

`CookieStore`'s `GetCookieHeader` and `StoreFromResponse` (and every other member a transfer calls) can be called from several transfers at once without losing, duplicating or corrupting a cookie, so a `-Z` run can share one store across its transfers as ADR-0127 requires.

## Context

- ADR-0127 §5 (BL-518): every transfer of a run shares one `CookieStore` (ADR-0126), and under `-Z` transfers run concurrently; `Curl.Cookies.UnitLibrary/CookieStore.cs` has no lock today.
- BL-519 builds the parallel runner and depends on this task.
- Rules: BCL only (`lock` or `System.Threading.Lock`), no `.Result`/`.Wait()`, the store keeps taking the time it acts at rather than reading a clock.

## Acceptance criteria

- [ ] `Curl.Cookies.UnitTests` has a test that stores distinct cookies from many concurrent callers (`Parallel.ForEachAsync` or several `Task.Run`s) and then finds every one in the store and in `GetCookieHeader`, and one that reads headers while another caller stores, without an exception.
- [ ] Existing `Curl.Cookies.UnitTests` pass unchanged.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
