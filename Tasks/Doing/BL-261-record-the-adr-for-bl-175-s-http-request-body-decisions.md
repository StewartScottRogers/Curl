---
id: BL-261
title: Record the ADR for BL-175's HTTP request body decisions
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-175]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed:
---
# BL-261 — Record the ADR for BL-175's HTTP request body decisions

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", records the four request-body decisions BL-175 took.

## Context

- BL-175 made these decisions and recorded them in its Notes, but could not write the ADR: `Documentation/Planning/Decisions` was in the `touches` of BL-154, then in Doing on another lane.
- The decisions, all measured on curl 8.21.0 (mingw, Windows reference) and listed with their commands in BL-175's Notes:
  1. `--json` reaches the handler as a `BytesBody` with Content-Type `application/json` plus the headers `Content-Type: application/json` and `Accept: application/json` appended after every `-H` (each only when no `-H` names it), because curl sends them in that position.
  2. A `StreamBody` of unknown length that fails a read ends the chunked body there, as curl's reader treats a failed read as end of file; one of known length fails with exit 26 and `client mime read EOF fail, only N/M of needed bytes read`.
  3. A final status that arrives during the 1-second `100 Continue` wait leaves the body unsent and is written as the response; the 417 retry is BL-260.
  4. `TransferReport.RequestSize` counts body bytes sent as well as the head, because `%{size_request}` does (151 for `-d x=1`).

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions` states the four decisions above, each with the measured curl behaviour it follows, and is marked "Decided by Claude under Stewart's delegation".
- [ ] The ADR's alternatives section says why each alternative lost.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
