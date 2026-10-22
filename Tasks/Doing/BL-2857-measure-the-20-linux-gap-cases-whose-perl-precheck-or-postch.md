---
id: BL-2857
title: Measure the 20 Linux gap cases whose perl precheck or postcheck the gap tool defeats: 608, 610, 611, 612, 615, 624, 625, 627, 638, 639, 762, 1027, 1082, 1083, 1291, 1443, 1444, 1445, 1583 and 1683
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools/Measure-UpstreamCases.cs]
lane: no
requirement: none
created: 2026-10-19
completed:
---
# BL-2857 — Measure the 20 Linux gap cases whose perl precheck or postcheck the gap tool defeats: 608, 610, 611, 612, 615, 624, 625, 627, 638, 639, 762, 1027, 1082, 1083, 1291, 1443, 1444, 1445, 1583 and 1683

## Goal

The gap office's measurement on Windows, Linux and macOS runs the perl prechecks and postchecks of its cases through the runner's C# interpreters, instead of reporting them `unmeasured | harness-unsupported`. Gap run 2026-10-22_0148 has 27 such cases on Windows: 608, 610, 611, 612, 613, 614, 615, 624, 625, 627, 638, 639, 762, 1013, 1014, 1022, 1023, 1027, 1082, 1083, 1291, 1443, 1444, 1445, 1446, 1583 and 1683. Linux and macOS have the same 27 plus 2072, 28 each. (The title's 20 Linux cases come from scorecard 2026-10-19_1329.)

## Context

Gap scorecard 2026-10-19_1329 lists 31 cases as `unmeasured | harness-unsupported` on Linux, and 20 of them are already on the ratchet and pass on Windows: the 20 above. The Linux evidence reads `the harness does not interpret the <client><precheck> line /usr/bin/perl -e '...'` (or `... /usr/bin/perl /home/runner/.cache/curl-gap/upstream/8.22.0/tests/libtest/test613.pl prepare ...`). The cause is visible in the code: `Gap/Tools/Measure-UpstreamCases.cs` substitutes `%PERL` with the absolute path of the perl it found (`FindPerl`, `SubstituteHostVariables`: `/usr/bin/perl` on the Linux runner), while the runner's interpreters `UpstreamPerlOneLiner`, `UpstreamPerlCheckLine`, `UpstreamTest610Script` and `UpstreamTest613Script` (`Curl.Conformance.UnitLibrary`) recognise only a line that begins with the bare program name `perl` (`UpstreamPerlOneLiner.Program` is `"perl"`, and the runner's own `%PERL` variable is that). The tool does the same on Windows (the path of Git for Windows' `usr/bin/perl.exe`), while this repository's own ratchet run, where the runner binds `%PERL` itself, passes all 20: so check the Windows scorecard's `unmeasured` list too (`Gap/Scorecards/2026-10-19_1329.md`) and fix every platform.

This also hides cases elsewhere: gap case 8's Linux evidence is the same `/usr/bin/perl` line, so more `excluded` cases may be `excluded` for this reason only. Gap paths and the gap workflow are interactive only (root CLAUDE.md): this task is `lane: no`.

Fix: stop substituting `%PERL` in the gap tool (the runner binds its own `%PERL` to the name its C# interpreters recognise), or substitute `perl` for it, and keep real-perl lookups only where the tool itself needs a perl. Extend the tool's `-SelfTest` (`Check(Substitute("%PERL %SRCDIR/x.pl", "/usr/bin/perl") == ...)` at about line 332) to pin the new behaviour. Then run `gap-platforms.yml` (`gh workflow run gap-platforms.yml`, interactive session) and read Linux's `harness-unsupported` count.

## Acceptance criteria

- [x] `Gap/Tools/Measure-UpstreamCases.cs` leaves `%PERL` for the runner to bind, and no longer substitutes a perl path (`FindPerl` is gone). Its `--self-test` fails on the old substitution (`%PERL is left for the runner ...`) and passes on the new one. It also runs a `%PERL -e` precheck case and a `%PERL ... test610.pl` precheck case through the runner as the tool leaves them.
- [x] A local Windows run of the tool on the 8.22.0 `tests/data`, converted by `ConvertTo-BehaviourMeasurement.ps1`, measures all 27 Windows cases in the Goal as `match`. The old tool measures them as `unmeasured | harness-unsupported`.
- [x] A `gap-platforms.yml` run of the change measures the 28 Linux and macOS cases in the Goal as `match` (or, per case, `excluded` with a reason). On each platform, the count of `unmeasured | harness-unsupported` cases falls from 30 to at most 2. The run's id and the counts are in Notes.
- [x] Any `excluded` case that this fix reveals as a gap is left to the gap office's own triage (`Gap/Triage.md`); this task files nothing in `Gap/Findings`.
- [x] The change is made on the `gap` branch's guarded path (interactive session only), and its pull request gap -> master is open for the session to merge under the standing exception once `CI` is green.

## Notes

- Fix: gap PR #195 (merged 2026-10-22 09:27 Arizona); master merged into work/dark-factory as 596dbf3a6.
- Windows, local re-measure: the 27 cases went from `unmeasured | harness-unsupported` to `match`; 1454, 375 and 2090 stay unmeasured (they need Curl built without IPv6, proxy or Schannel).
- gap-platforms.yml run 39803629751: Linux and macOS each measure match 1488, gap 0, unmeasured 2 (1454, 375), excluded 582. Before: 30 unmeasured on each.
- Nothing was filed in Gap/Findings.

## Log

- 2026-10-19: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 85c52118 5909e2b3 3c42afd5 01b4bff2 23b442b3.
