---
id: BL-384
title: Record the ADR for how Curl.Console draws the progress meter's status lines
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-131]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-384 — Record the ADR for how Curl.Console draws the progress meter's status lines

## Goal

An ADR in `Documentation/Planning/Decisions` records the four progress-meter decisions BL-131 made, marked "Decided by Claude under Stewart's delegation", and is listed in that folder's `README.md`.

## Context

BL-131 made `Curl.Console` draw the progress meter's status lines from the handler's byte reports (`TransferProgressRecorder`, `ProgressMeterFields`). It could not write the ADR itself: `Documentation/Planning/Decisions` was in the `touches` of BL-256, running at the same time on another dark factory lane. The decisions, and why, are in BL-131's Notes (`Tasks/Done/.../BL-131-*.md`); copy them, do not re-decide them:

1. The meter is still written after the transfer, so standard error's bytes are curl's but a terminal does not see the line move.
2. A successful transfer whose handler reported bytes gets three done draws; a failed one gets one `Curl_pgrsDone` update, which draws only a second or more after the last speed sample.
3. A handler that reports no bytes (`file://` today) gets no end draws, so BL-102's `file://` bytes are unchanged.
4. curl's same-second check in `progress_calc` (`lastshow`) is not modelled, because no draw in this call sequence can reach it.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-NNNN-*.md` (next free number) states the four decisions above, each with its reason, and is marked "Decided by Claude under Stewart's delegation".
- [x] `Documentation/Planning/Decisions/README.md` lists it.

## Notes

- ADR-0092 is the next free number (ADR-0091 was the highest on 2026-09-27). The four decisions and their reasons are copied from BL-131's Notes, not re-decided. Docs only, no `.cs` or project file changed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0092 records BL-131's four progress-meter decisions and is listed in the Decisions README
