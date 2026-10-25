---
id: BL-3501
title: Keep the conformance auditor from refiling platform differences an ADR already decided
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Instructions/Conformance.md]
lane: no
requirement: none
created: 2026-10-25
completed:
---
# BL-3501 — Keep the conformance auditor from refiling platform differences an ADR already decided

## Goal

The conformance auditor no longer files a difference from the reference curl that an accepted ADR keeps on purpose, such as `--http2` on Windows (ADR-0786).

## Context

The conformance auditor filed `--http2` / `--http2-prior-knowledge` accepted on Windows three times: AF-0381, AF-0423 and AF-0432, each rejected by Stewart because ADR-0786 keeps HTTP/2 on where the Schannel curl refuses it. The gap office already records these as `decided:ADR-0786` in `Gap/Baselines/platform-exclusions.json`. The method in `Audit/Instructions/Conformance.md` has no such list.

## Acceptance criteria

- [ ] `Audit/Instructions/Conformance.md` tells the auditor to read `Gap/Baselines/platform-exclusions.json` and not to report a difference it lists for that platform, naming the ADR in its summary instead.
- [ ] The instruction says a difference outside the list is still reported, and that the list changes only by ADR.
- [ ] Done on the `audit` branch; its pull request is merged once CI passes on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: 82568fda 65e0fd9f 68e59f8c.
