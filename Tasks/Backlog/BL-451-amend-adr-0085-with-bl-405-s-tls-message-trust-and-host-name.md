---
id: BL-451
title: Amend ADR-0085 with BL-405's TLS message, trust and host-name events
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-405]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed:
---
# BL-451 — Amend ADR-0085 with BL-405's TLS message, trust and host-name events

## Goal

ADR-0085 carries an amendment, "Decided by Claude under Stewart's delegation", recording
the decisions BL-405 made, and its Decision section no longer says those lines are left out.

## Context

- BL-405 could not edit `Documentation/Planning/Decisions`: BL-380, in Doing at the time,
  touched it. The decisions are written out in full under BL-405's Notes ("Decisions");
  copy them into an `## Amendment (BL-405, 2026-09-27)` section, as BL-404's amendment does.
- The last bullet of ADR-0085's Decision section ("The handshake event carries no host
  name, ... are left to follow-up tasks") is now false for `Curl.Output`; reword it so it
  says what is still left (producers: BL-452; `--trace`: BL-450).
- BL-452 (Networking reports trust, checked host name and proxy handshakes) could not edit
  the ADR either: BL-437 touched `Documentation/Planning/Decisions` while it ran. Its
  decisions are under BL-452's Notes ("Decisions"); add them as an
  `## Amendment (BL-452, 2026-09-27)` section.

## Acceptance criteria

- [ ] ADR-0085 has an `Amendment (BL-405, ...)` section stating each decision in BL-405's
      Notes, marked "Decided by Claude under Stewart's delegation".
- [ ] No sentence in ADR-0085 says `Curl.Output` leaves out the TLS record, `SSL Trust`,
      `subjectAltName` or `Proxy certificate:` lines.
- [ ] ADR-0085 has an `Amendment (BL-452, ...)` section stating each decision in BL-452's
      Notes, marked "Decided by Claude under Stewart's delegation", and no sentence in it
      says the HTTPS proxy's handshake is not reported.

## Notes

- Filed from BL-405 (2026-09-27).

## Log

- 2026-09-27: Created.
