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
completed: 2026-09-27
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

- [x] ADR-0085 has an `Amendment (BL-405, ...)` section stating each decision in BL-405's
      Notes, marked "Decided by Claude under Stewart's delegation".
- [x] No sentence in ADR-0085 says `Curl.Output` leaves out the TLS record, `SSL Trust`,
      `subjectAltName` or `Proxy certificate:` lines.
- [x] ADR-0085 has an `Amendment (BL-452, ...)` section stating each decision in BL-452's
      Notes, marked "Decided by Claude under Stewart's delegation", and no sentence in it
      says the HTTPS proxy's handshake is not reported.

## Notes

- Filed from BL-405 (2026-09-27).
- 2026-09-27, lane 2: added `Amendment (BL-405, 2026-09-27)` and `Amendment (BL-452,
  2026-09-27)` to ADR-0085, copied from those tasks' Notes. Reworded the last Decision
  bullet: it now says what `Curl.Output` writes (BL-405), what `--trace` writes (BL-450,
  Done), what `Curl.Networking` reports (BL-452) and the one gap left, TLS records that
  `SslStream` does not expose. Reworded BL-404's amendment sentence about the unreported
  HTTPS proxy handshake into past tense pointing at BL-452, to meet the third criterion.
  Docs only; no code changed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0085 records BL-405's and BL-452's TLS message, trust, host-name and proxy decisions as amendments
