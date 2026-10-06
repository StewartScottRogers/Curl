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
completed: 2026-09-28
---
# BL-755 — Make CookieStore safe for concurrent transfers

## Goal

`CookieStore`'s `GetCookieHeader` and `StoreFromResponse` (and every other member a transfer calls) can be called from several transfers at once without losing, duplicating or corrupting a cookie, so a `-Z` run can share one store across its transfers as ADR-0127 requires.

## Context

- ADR-0127 §5 (BL-518): every transfer of a run shares one `CookieStore` (ADR-0126), and under `-Z` transfers run concurrently; `Curl.Cookies.UnitLibrary/CookieStore.cs` has no lock today.
- BL-519 builds the parallel runner and depends on this task.
- Rules: BCL only (`lock` or `System.Threading.Lock`), no `.Result`/`.Wait()`, the store keeps taking the time it acts at rather than reading a clock.

## Acceptance criteria

- [x] `Curl.Cookies.UnitTests` has a test that stores distinct cookies from many concurrent callers (`Parallel.ForEachAsync` or several `Task.Run`s) and then finds every one in the store and in `GetCookieHeader`, and one that reads headers while another caller stores, without an exception.
- [x] Existing `Curl.Cookies.UnitTests` pass unchanged.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Design (decided in the run, no ADR: ADR-0127 §5 already requires the shared store to be safe; this is
  how): one `System.Threading.Lock` in `CookieStore`. `GetCookieHeader`, `AddCookieString`, the store
  and expiry steps of `StoreFromResponse` and `LoadCookieFile`, and the snapshot `WriteCookieJar` writes
  all run under it. `Cookies` now returns a copy taken under the lock rather than the live list, so a
  caller enumerating it cannot see it change underneath. Parsing (`SetCookieParser`, `NetscapeCookieFile.Read`),
  `-v` reporting and jar file I/O happen outside the lock, so a slow `ITransferEvents` sink or file never
  holds other transfers up and a sink that calls back into the store cannot deadlock. To report outside
  the lock, `MayStore` became `RefusalOf` (returns the `-v` line or null) and `StoreReceived` returns a
  `ReceivedCookieOutcome`; the lines and their order are unchanged.
- One response's cookies are stored one header at a time, so two concurrent responses' cookies may
  interleave in store order; each cookie is stored whole. That matches curl, whose `-Z` transfers share
  one cookie engine and store each `Set-Cookie` as it is read.
- Tests: `CookieStoreTests.Concurrency.cs` (3 tests: 100 concurrent `StoreFromResponse` callers,
  headers/jar/`Cookies` read while another caller stores, concurrent `AddCookieString` and
  `LoadCookieFile`). Cookies tests 339 -> 342. `Measure-CodeQuality.ps1 -Library Curl.Cookies.UnitLibrary`:
  100% line, 100% branch, 101 members, 0 failing, worst CRAP 10.
- The solution-wide fast run once reported 1 failure in `Curl.Core.UnitTests` (which does not reference
  Cookies); three reruns of that project were all green, so it is load-sensitive, not this change.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. CookieStore takes one lock around every read and change of its cookies, so -Z transfers can share it
