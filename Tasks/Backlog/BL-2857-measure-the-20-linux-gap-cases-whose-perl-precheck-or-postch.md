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

The gap office's Linux and macOS measurement runs the perl prechecks and postchecks of cases 608, 610, 611, 612, 615, 624, 625, 627, 638, 639, 762, 1027, 1082, 1083, 1291, 1443, 1444, 1445, 1583 and 1683 through the runner's C# interpreter instead of reporting them `unmeasured | harness-unsupported`.

## Context

Gap scorecard 2026-10-19_1329 lists 31 cases as `unmeasured | harness-unsupported` on Linux, and 20 of them are already on the ratchet and pass on Windows: the 20 above. The Linux evidence reads `the harness does not interpret the <client><precheck> line /usr/bin/perl -e '...'` (or `... /usr/bin/perl /home/runner/.cache/curl-gap/upstream/8.22.0/tests/libtest/test613.pl prepare ...`). The cause is visible in the code: `Gap/Tools/Measure-UpstreamCases.cs` substitutes `%PERL` with the absolute path of the perl it found (`FindPerl`, `SubstituteHostVariables`: `/usr/bin/perl` on the Linux runner), while the runner's interpreters `UpstreamPerlOneLiner`, `UpstreamPerlCheckLine`, `UpstreamTest610Script` and `UpstreamTest613Script` (`Curl.Conformance.UnitLibrary`) recognise only a line that begins with the bare program name `perl` (`UpstreamPerlOneLiner.Program` is `"perl"`, and the runner's own `%PERL` variable is that). The tool does the same on Windows (the path of Git for Windows' `usr/bin/perl.exe`), while this repository's own ratchet run, where the runner binds `%PERL` itself, passes all 20: so check the Windows scorecard's `unmeasured` list too (`Gap/Scorecards/2026-10-19_1329.md`) and fix every platform.

This also hides cases elsewhere: gap case 8's Linux evidence is the same `/usr/bin/perl` line, so more `excluded` cases may be `excluded` for this reason only. Gap paths and the gap workflow are interactive only (root CLAUDE.md): this task is `lane: no`.

Fix: stop substituting `%PERL` in the gap tool (the runner binds its own `%PERL` to the name its C# interpreters recognise), or substitute `perl` for it, and keep real-perl lookups only where the tool itself needs a perl. Extend the tool's `-SelfTest` (`Check(Substitute("%PERL %SRCDIR/x.pl", "/usr/bin/perl") == ...)` at about line 332) to pin the new behaviour. Then run `gap-platforms.yml` (`gh workflow run gap-platforms.yml`, interactive session) and read Linux's `harness-unsupported` count.

## Acceptance criteria

- [ ] `Gap/Tools/Measure-UpstreamCases.cs` no longer leaves a line the runner cannot interpret: its self-test fails on the old `%PERL` substitution and passes on the new one.
- [ ] A local run of the tool on Windows with a fake `perl` path of `/usr/bin/perl` (the way the Linux runner sets it) measures case 610 as `match`.
- [ ] After the change, a `gap-platforms.yml` run shows the 20 cases listed in the Goal measured `match` on Linux and macOS (or, per case, `excluded` with a reason), and the count of `unmeasured | harness-unsupported` cases on Linux falls from 31 to at most 11; the run's id and the counts are in Notes.
- [ ] Any `excluded` case that this fix reveals as a gap is left to the gap office's own triage (`Gap/Triage.md`); this task files nothing in `Gap/Findings`.
- [ ] The change is made on the `gap` branch's guarded path (interactive session only), with its pull request merged under the standing exception once `CI` is green.

## Notes

## Log

- 2026-10-19: Created.
