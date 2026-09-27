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
completed:
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

- [ ] `RedirectFollowerTests` prove `result.Report.RedirectUrl` is null after each of the three refusals above and still `http://[bad` after the exit-47 limit refusal.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-09-26: Created.
