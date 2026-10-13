---
id: BL-2128
title: Move the gap office's target to curl 8.22.0 and file the tasks of the 8.22.0 findings
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2125, BL-2126, BL-2127]
touches: [Gap/Baselines/target.json, Gap/Findings]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2128 — Move the gap office's target to curl 8.22.0 and file the tasks of the 8.22.0 findings

## Goal

The gap office targets curl 8.22.0: `Gap/Baselines/target.json` says `8.22.0`, the 8.22.0
release findings are target-scope, a gap run has measured against 8.22.0, and their
`Close GF-####` tasks are on the board.

## Context

ADR-0479 step 3.4, carrying out ADR-0433 decision 6 for curl 8.22.0. Interactive only
(`lane: no`): it writes `Gap/`, which is done on the `gap` branch and merged under the standing
exception in `CLAUDE.md` once CI is green.

On 2026-10-13 the newest-scope findings were GF-0038 (behaviour, open: 62 cases added, 89
changed, 3 removed), GF-0039 (features, closed: TLS-SRP removed) and GF-0040 (options, open:
`--httpsig-algo`, `--httpsig-headers`, `--httpsig-key`, `--httpsig-keyid`, RFC 9421 HTTP
Message Signatures). Waits on BL-2125 (reference curl is 8.22.0), BL-2126 (upstream test data
at `curl-8_22_0`, so the 62 added cases are measured) and BL-2127 (Curl prints 8.22.0), so the
first 8.22.0 run measures the real gap. Expect the behaviour score to drop at that run: the
added cases are measured for the first time (ADR-0479, Consequences).

Steps: set `target.json` to `{ "version": "8.22.0", "decidedBy": "ADR-0479" }`; make sure
`Gap/Upstream/8.22.0/` holds the inventories; change GF-0038, GF-0039 and GF-0040 from
`scope: newest` to `scope: target` with a Log line naming ADR-0479; run
`Gap\RunGapAnalysis.cmd -NewTab`; then run `Gap\Tools\New-TasksFromGaps.ps1` per
`Gap/Triage.md` against a freshly fetched `work/dark-factory`. The `--httpsig-*` options are a
hand-built feature (HMAC, RSA-PSS, ECDSA and Ed25519 signing over RFC 9421's signature base);
their task follows CLAUDE.md's rule that such a piece lives in its own `Curl.<Area>.UnitLibrary`.

## Acceptance criteria

- [ ] `Gap/Baselines/target.json` on `master` names version `8.22.0` and `decidedBy` `ADR-0479`.
- [ ] GF-0038, GF-0039 and GF-0040 carry `scope: target`, each with a Log line citing ADR-0479.
- [ ] A scorecard under `Gap/Scorecards/` shows `Target: 8.22.0` and a reference whose first line names curl 8.22.0.
- [ ] Every open `scope: target` finding after that run has a `Close GF-####` task on `work/dark-factory`'s board, written into the finding's `tasks`, including one for the four `--httpsig-*` options.

## Notes

- 2026-10-13: the Windows reference is now Git 2.56.0's `ucrt64\bin\curl.exe` (curl 8.22.0), a **debug-enabled** build. Its `-V` starts with `WARNING: this libcurl is Debug-enabled, do not use in production` and its Features list `Debug`. When the target moves, strip that warning line from the reference's `-V` before comparing (the version and features measurements), and keep `Debug` excluded as `debug-build-only`. Normal transfers print no extra stderr (checked with Record-CurlExchange.ps1, BL-2125).

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing. interactive session
