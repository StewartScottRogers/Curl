---
id: BL-2038
title: Keep the upstream case harness's long absolute LOGDIR from wrapping curl's Note and Warning lines where runtests.pl's short log/ does not
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2038 — Keep the upstream case harness's long absolute LOGDIR from wrapping curl's Note and Warning lines where runtests.pl's short log/ does not

## Goal

Upstream tests 994, 996 and 1491 pass in `UpstreamConformanceTests` and are listed on
`Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, because the harness no longer lets its long
absolute `%LOGDIR` wrap a `Note:` or `Warning:` line that runtests.pl's short `log` keeps on one line.

## Context

- Split from BL-2006 (gap finding GF-0013). Curl already prints curl 8.21.0's
  `Note: skips transfer, "<file>" exists locally` under the `--trace-ascii`/`--trace-time` options
  runtests.pl adds (BL-493, BL-1806, ADR-0447), and BL-2017 made `UpstreamCaseRunner` pass them.
- What is left (measured 2026-10-10, ratchet run in lane 2): test996's verdict is
  `<verify><stderr> differs at byte 22 (line 1): expected "Note: skips transfer, \"Z:/repos/.../log/test996-<guid>/there\" exists locally\r\n", got "Note: skips transfer, \r\n"`;
  994 and 1491 the same. Curl wraps the note at 79 columns exactly as curl's `voutf` does
  (cut at the last blank, `Note: ` repeated on the next line). Upstream never wraps it because
  runtests.pl sets `COLUMNS=79` (runtests.pl line 172) with `$LOGDIR = "log"` (globalconfig.pm
  line 113), so `log/there` fits; the harness's `%LOGDIR` is absolute (`UpstreamCaseRunner.Variables`,
  ADR-0458) and about 100 characters long.
- Options (decide, ADR "Decided by Claude under Stewart's delegation"): (a) in verification, when
  the expected `<stderr>` names the case's `%LOGDIR`, un-wrap curl's warnf/notef wrapping in both
  expected and actual stderr (a line ending in a blank followed by a line starting with the same
  `Note: ` or `Warning: ` prefix is joined, the second prefix dropped) before comparing; (b) give the
  run a `COLUMNS` widened by the length the absolute `%LOGDIR` adds over `log`, when the case sets
  no `COLUMNS` (simpler, but widens lines that do not name `%LOGDIR`, so check no listed case that
  pins a 79-column wrap breaks). Prefer (a): it changes only lines that name the log directory.
- The gap office's `Gap/Tools/Measure-UpstreamCases.cs` measures the GF-0013 items; whether it runs
  cases through this harness could not be checked from a lane (the guard refuses `Gap/`).

## Acceptance criteria

- [ ] `dotnet test Curl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCase_RunThroughCurl_HoldsTheRatchet"` passes with 994, 996 and 1491 on `PassingUpstreamCases.txt`.
- [ ] A test in `Curl.Conformance.UnitTests` pins the chosen rule from inline test-file text (a wrapped `Note:` naming `%LOGDIR` matches upstream's unwrapped line).
- [ ] An ADR records the choice.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
