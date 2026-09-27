---
id: BL-289
title: Empty %{redirect_url} when -L refuses a redirect target that does not parse or whose scheme is refused
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-271]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-289 — Empty %{redirect_url} when -L refuses a redirect target that does not parse or whose scheme is refused

## Goal

Under `-L`, when `RedirectFollower` refuses a redirect target because it does not parse (exit 3), its scheme is one curl cannot parse (exit 1) or its scheme is disabled in redirects (exit 1), the merged report has `RedirectUrl` null so `%{redirect_url}` writes nothing, as curl 8.21.0 does.

## Context

- Found in BL-271 (2026-09-26). Measured against curl 8.21.0 (Schannel, Windows) with a local server replying `302` and `-w '[%{redirect_url}]'`:
  - `Location: http://[bad` -> exit 3, `[]`.
  - `Location: foo://x` -> exit 1, `[]`.
  - `Location: file:///C:/Windows/win.ini` -> exit 1, `[]`.
  - `Location: http://[bad` with `--max-redirs 0` -> exit 47, `[http://[bad]` (the limit refusal keeps it).
- Without `-L` curl reports the value whole (BL-179), so only the follower's refusals clear it.
- `Curl.Core.UnitLibrary/RedirectFollower.cs`, `FollowChainAsync`: the refusal path returns `chain.Merge(...)` carrying the last hop's `RedirectUrl`.

## Acceptance criteria

- [x] `RedirectFollowerTests` prove `result.Report.RedirectUrl` is null after each of the three refusals above and still `http://[bad` after the exit-47 limit refusal.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

- `Refusal` now returns `KeepsRedirectUrl` beside the exit code and message: `true` only for the
  exit-47 limit refusal, `false` for the unparsable target (exit 3) and both scheme refusals
  (exit 1). `StopBeforeHop` reports it to `RedirectChain.Refused`, and `Merge` clears
  `RedirectUrl` when it was not kept. Values follow the curl 8.21.0 measurements in Context;
  no new decision, so no ADR.
- A hop proxy selector failure is not a target refusal and keeps `RedirectUrl` as before
  (default: unchanged behaviour, not measured in this task).
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Core.UnitTests 771 passed,
  3 skipped); `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` 100% line and branch,
  0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -L writes an empty %{redirect_url} after refusing an unparsable or refused-scheme target; the exit-47 limit keeps it
