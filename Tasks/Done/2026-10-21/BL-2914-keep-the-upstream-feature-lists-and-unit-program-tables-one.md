---
id: BL-2914
title: Keep the upstream feature lists and unit-program tables one entry per line and merge=union so tasks add to them side by side
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamCurlVersionLineFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamHarnessFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamServedProtocolFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamToolFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamUnixFeatures.cs, Curl.Conformance.UnitLibrary/UpstreamWindowsFeatures.cs, .gitattributes, Tasks/SharedListFiles.txt]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2914 — Keep the upstream feature lists and unit-program tables one entry per line and merge=union so tasks add to them side by side

## Goal

Tasks that each add a feature name or a ported unit program to a registration table no longer count as overlapping, so they run in parallel.

## Context

Stewart, 2026-10-21: "always function in a highly parallel fashion". After BL-2885 split UpstreamCurlPlatform.cs, `task-board.ps1 hotspots` showed 8 tasks waiting on UpstreamPlatformFeatureTable.cs, because each adds one line to a feature list. One-line additions merge cleanly with `merge=union` (as the ratchet does) once every entry is on its own line.

## Acceptance criteria

- [x] The six Upstream*Features.cs lists hold one name per line, same names in the same order.
- [x] `.gitattributes` marks them, UpstreamPlatformFeatureTable.cs and UnitPrograms.*.cs `merge=union`.
- [x] `Tasks/SharedListFiles.txt` lists the same files, for task-board.ps1 to leave out of the overlap check (the reading side is the guard pull request `guard/shared-list-files`).
- [x] CLAUDE.md states the rule.
- [x] Curl.Conformance.UnitTests builds clean and its fast tests pass (3731 passed, 0 failed).

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 10b4d608 9979d33e 5fcc9165 1a4376bd 490f1e94.
- 2026-10-21: Doing -> Done. Lists reformatted, merge=union and SharedListFiles.txt in place; Conformance fast tests green
