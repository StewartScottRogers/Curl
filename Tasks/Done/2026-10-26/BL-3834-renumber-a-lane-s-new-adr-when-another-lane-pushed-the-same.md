---
id: BL-3834
title: Renumber a lane's new ADR when another lane pushed the same number first
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, DarkFactory/Integrate.ps1, DarkFactory/AdrIds.ps1, DarkFactory/LaneRules.md]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3834 — Renumber a lane's new ADR when another lane pushed the same number first

## Goal

When a lane integrates, any ADR it adds whose number another commit on the shared branch already uses is renumbered to the next free number before the push, so no two ADRs share a number.

## Context

Shift 20261026-063535 (9 lanes, gap report tasks): by 08:37 Arizona on 2026-10-26, `Documentation/Planning/Decisions` on `work/dark-factory` held six ADR-0815s, seven ADR-0816s and four ADR-0817s, and 14 numbers in all are shared. Each lane picks the next ADR number from its own worktree, as task IDs once did; tasks are fixed by `task-board.ps1 dedupe -Since` after the rebase (`DarkFactory/Claim.ps1` around line 151, `DarkFactory/TaskIds.ps1`), but nothing does the same for ADRs. BL-326 renumbered a duplicate ADR-0040 by hand.

Do the same as task dedupe: after the rebase in `DarkFactory/Integrate.ps1`, ADR files present at the rebase base keep their numbers; an ADR the lane's commits add that shares a number with another file gets the next free number, its file name and `# ADR-####` heading change, and every reference to the old number in files the lane's commits changed is rewritten. Put the logic in its own `DarkFactory/AdrIds.ps1` (loaded by the loader in its fixed order, BL-2988) with a self-test, as `TaskIds.ps1` does. Add the rule to `DarkFactory/LaneRules.md` if lanes need to know it.

## Acceptance criteria

- [x] A scratch-repository test in `AdrIds.ps1`'s self-test: two branches each add `ADR-0900-*.md` and cite it from a task file; after the second is rebased and integrated, its ADR is `ADR-0901-*.md`, its heading and its citing task say ADR-0901, and the first branch's ADR-0900 is unchanged.
- [x] An ADR present at the rebase base is never renumbered, even if it already shares its number (that cleanup is BL-3835).
- [x] A failure in the renumbering stops the push and says why, as a failed task dedupe does (BL-3491).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes
- Touches widened by `Curl.Protocol.Http.UnitLibrary/HttpResponseHeadBuilder.cs`: the head's bytes (what `-i`/`-D` write) come from the builder, so the lone `\r` is kept there; no task in Doing on origin/work/dark-factory names it or the whole library.
- Cause: `HttpLineReader` drops the unfinished bytes at close, so the head ended on `Content-Length: N\r\n` and BL-483's rule (curl never acts on the header the head ends on at close) left the Content-Length out. Fix (ADR-0817): `HttpResponseHeadReader.EndHeadAtClose` takes the unfinished bytes and, when `ShowsPendingHeaderWhole` says they show the last header whole, acts on the whole head; `HttpResponseHeadBuilder.EndHeadAtClose` keeps an unfinished line that is exactly `\r` in the head bytes.
- Measured 2026-10-26 with `Record-CurlExchange.ps1` on 127.0.0.1:21100, REF = curl 8.22.0 (ucrt64, Schannel), PORT = Curl.Console after the fix; request.bin hash identical in every run, no files written:
  - `-sSi`, `HTTP/1.1 200 OK\r\nContent-Length: 3\r\nX`: REF and PORT exit 18, `curl: (18) transfer closed with 3 bytes remaining to read`, stdout 36 bytes, same hash (PORT before the fix: exit 0, empty stderr).
  - `-sSi`, `...Content-Length: 3\r\n\r`: both exit 18, same message, stdout 37 bytes ending `0D 0A 0D`, same hash (before: exit 0, 36 bytes).
  - `-sSf`, `HTTP/1.1 404 NF\r\nContent-Length: 3\r\nX`: both exit 18, same message, stdout empty (before: exit 22, `The requested URL returned error: 404`).
  - `-sSi`, `/closehdr` = `HTTP/1.1 200 OK\r\nContent-Length: 10\r\nX-Foo: ba`: both exit 18, `transfer closed with 10 bytes remaining to read`, stdout 37 bytes, same hash (before: exit 0).
  - Already matching: `-sSi`, `HTTP/1.1 200 OK\r\nX-Foo: 1\r\nX` (no Content-Length): both exit 0, stdout 27 bytes, same hash, before and after.
- Focused tests `Cgr027Tests.cs`: with the library change reverse-applied, 4 of 5 fail (the no-Content-Length guard passes); with it, all pass.
- Platform: the tests use scripted connections only, no paths or OS text, so they hold on Linux and macOS; the report confirmed the same curl behaviour on Linux.
- `dotnet build` clean, fast tests green (exit 0), `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members (a first run found `ReadAsync` at complexity 12; the condition moved to `HeadWholeAt`).

- Decided in ADR-0818. `Invoke-AdrRenumber` (DarkFactory/AdrIds.ps1) runs in Update-BoardBeforePush after the task dedupe and rewrites old numbers only on lines the lane added, so another lane's README row keeps its number. Update-BoardBeforePush now returns a reason and both push paths stop on it. `-TestAdrIds` covers the criteria; `-TestIntegrateLock` still passes.
- Added RunDarkFactory.ps1 (loader entry, -TestAdrIds switch) to the files touched; no other Doing task named it.
- No C# library changed, so Measure-CodeQuality does not apply; dotnet build is clean and the fast tests are green.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: a2edeb28 67279eb4 01381fd7 779790c0.
- 2026-10-26: Doing -> Done. ADR renumbering on integrate (ADR-0818)
