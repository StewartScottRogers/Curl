---
id: BL-2888
title: Say in the conformance CLAUDE.md that a unittest case runs its ported unit program
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2841]
touches: [Curl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-21
completed:
---
# BL-2888 — Say in the conformance CLAUDE.md that a unittest case runs its ported unit program

## Goal

`Curl.Conformance.UnitLibrary/CLAUDE.md` says how a `unittest` case is run, as the code does since BL-2841.

## Context

BL-2841 (ADR-0711) made `UpstreamCaseRunner` run a case with the `unittest` feature through a
ported `IUnitProgram` from `UnitPrograms` (`Curl.Conformance.UnitProgram.UnitLibrary`), and skip it
with `Curl has no port of <program>` when none is registered. The libtest paragraph of the
library's `CLAUDE.md` still says any `unitNNNN` or `toolNNNN` tool skips with
`the harness does not act on <client><tool>`, which is now true only of a case without the
`unittest` feature. BL-2841 could not edit the file because BL-2880 held it in its `touches`.

## Acceptance criteria

- [ ] The libtest paragraph of `Curl.Conformance.UnitLibrary/CLAUDE.md` says that a `unittest` case runs the unit program its `<tool>` names (or `unit<testnumber>` with none) from the runner's optional `UnitPrograms`, that the platform reports `unittest`, `Debug` and `TrackMemory` to such a case only when its program is ported, and that an unported one skips with `Curl has no port of <program>`.
- [ ] The paragraph says `the harness does not act on <client><tool>` only for a `unitNNNN` or `toolNNNN` tool in a case without the `unittest` feature.

## Notes

## Log

- 2026-10-21: Created.
