---
id: BL-1001
title: Define the audit finding, auditor report and scorecard formats
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1000]
touches: [Audit/Findings, Audit/Scorecards, Audit/Instructions/Report-Format.md, Audit/Instructions/Auditor-Rules.md]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1001 — Define the audit finding, auditor report and scorecard formats

## Goal

Three fixed formats exist, each with a template: the finding file, the machine-readable report every auditor returns, and the scorecard, so the tools (BL-1016, BL-1017) and the auditors (BL-1009 to BL-1014) all write and read the same shapes.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1001`.
Design from the ADR recorded by BL-994.

**Finding** - `Audit/Findings/README.md` (the rules) and
`Audit/Findings/FINDING-TEMPLATE.md`. One file per finding,
`AF-####-<slug>.md`, IDs in one sequence never reused. Front matter: `id`, `title`,
`auditor` (`quality`, `security`, `performance`, `conformance`, `truthfulness`,
`process`), `severity` (`Critical`, `High`, `Medium`, `Low`, each defined in the README),
`status` (`proposed`, `accepted`, `rejected`, `closed`), `key` (the auditor's stable
dedupe key), `task` (`BL-###` or `none`), `found` (date), `found-at` (commit audited),
`scorecard` (the one that found it), `closed`, `closed-by` (the re-audit scorecard).
Body sections in order: `Summary`, `Evidence`, `Reproduction` (a command run from the
repository root, with the expected and the actual result), `Re-audits` (append-only, one
line per re-audit: date, scorecard, reproduces yes/no, evidence). Rules the README
states: a finding arrives `proposed`; only Stewart sets `accepted` or `rejected` (in the
audit pull request or by telling a session); it becomes `closed` only when a re-audit by
the same auditor, not flagged unreliable, reports the reproduction no longer reproduces -
never because its task reached Done; a closed finding that reappears is a new finding
naming the old one.

**Auditor report** - `Audit/Instructions/Report-Format.md`. Each auditor ends its reply
with one fenced `json` block: `{ "auditor", "commit", "fingerprint", "findings": [ {
"key", "title", "severity", "location", "evidence", "reproduction" } ], "reaudits": [ {
"finding", "reproduces", "evidence" } ], "metrics": { ... } }`. Define `key` (e.g.
`quality:Curl.Cli.UnitTests/ParserTests.cs:Parse_Empty_Throws:weak-assertion`, stable
across runs so the same issue is recognised again), and each auditor's `metrics` names:
performance - per scenario of BL-1007 (`startup`, `small-get`, `large-get`,
`headers-verbose`, `chunked`, `redirects`), `<scenario>.curl.medianMs`,
`<scenario>.curl.p90Ms`, `<scenario>.curl.medianPeakWorkingSetBytes` and the same three
for `<scenario>.candidate`, plus `candidateBinaryBytes`; process - `tasksDone`,
`medianTaskMinutes`, `p90TaskMinutes`, `tasksClaimedMoreThanOnce`, `requeues`,
`resumedRuns`, `ciRedMinutes`, `laneIdleMinutes`, `waitOverlapMinutes`,
`waitNothingReadyMinutes`, `tokensInput`, `tokensOutput`, `costUsd`,
`costUsdPerTaskDone` (defined in BL-1008); quality - `mutationScore.<Library>` per
library mutated; security - `fuzzIterations.<target>`, `fuzzCrashes.<target>`;
conformance - `differentialCases`, `differentialDifferences`; truthfulness - none
required. Give one complete example.

**Rules every auditor follows** - `Audit/Instructions/Auditor-Rules.md`, which each
auditor agent (BL-1009 to BL-1014) tells its auditor to read first:

1. Audit only the tree the prompt names (a detached worktree prepared by the audit run),
   never the checkout you were started in. Never change a tracked file in that tree;
   scratch work goes in the temporary folder the prompt names, or in a worktree a tool
   creates and removes itself.
2. Two phases. Phase 1 judges the code, tests and scripts alone: do not open
   `Documentation/Planning/Decisions/`, `Tasks/`, the factory's logs, or commit messages
   until every phase-1 finding is written down - except the sources your own
   instructions name as your subject (the process auditor audits the logs and history;
   the truthfulness auditor audits ADRs against the code), which you read as evidence,
   never as justification. Phase 2 may read them, and may only
   annotate a phase-1 finding ("explained by ADR-NNNN: ...") - never delete it; whether
   an ADR's reason is good enough is Stewart's call at triage.
3. Never open `Audit/PlantedDefects/`, `Audit/Findings/` or `Audit/Scorecards/`, and
   never read another auditor's output. The findings you are asked to re-audit are given
   in the prompt.
4. Every finding has evidence (file and line, command output, measurement) and a
   reproduction a stranger can run from the repository root. No evidence, no finding.
5. Re-audit each listed finding by running its reproduction and report whether it still
   reproduces.
6. End with exactly one report block in the format of `Report-Format.md`.
7. No Python; PowerShell or C# file-based apps only; no network beyond loopback.

**Scorecard** - `Audit/Scorecards/README.md` and `SCORECARD-TEMPLATE.md`. File name
`yyyy-MM-dd_HHmm.md`. Fixed sections in fixed order so two scorecards diff line by line:
header (date, audited branch and commit, auditor fingerprint, planted-defect count,
duration, token cost); one table with a row per auditor in the fixed order quality,
security, performance, conformance, truthfulness, process: model, new findings by
severity, re-audited and closed, still open, planted assigned, planted caught, catch
rate, reliable (yes/no); the performance table; the process table; "Change since the
previous scorecard" (the same numbers' deltas); links to the new finding files.

## Acceptance criteria

- [ ] `Audit/Findings/README.md` and `FINDING-TEMPLATE.md` exist with the front matter fields, statuses, severities (each defined in one sentence) and section order above, and the README states the closure rule in those words.
- [ ] `Audit/Instructions/Report-Format.md` defines the JSON block, the `key` rule and each auditor's `metrics` names, and its example parses with `ConvertFrom-Json`.
- [ ] `Audit/Instructions/Auditor-Rules.md` states the seven rules above.
- [ ] `Audit/Scorecards/README.md` and `SCORECARD-TEMPLATE.md` exist with the sections in the order above and the auditor rows in the fixed order.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
