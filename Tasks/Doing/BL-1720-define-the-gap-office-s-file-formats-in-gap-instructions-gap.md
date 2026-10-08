---
id: BL-1720
title: Define the gap office's file formats in Gap/Instructions/Gap-Format.md
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Gap/Instructions/Gap-Format.md, Gap/Findings/README.md, Gap/Findings/FINDING-TEMPLATE.md, Gap/Scorecards/README.md]
model: opus
requirement: none
created: 2026-10-08
completed:
---
# BL-1720 — Define the gap office's file formats in Gap/Instructions/Gap-Format.md

## Goal

`Gap/Instructions/Gap-Format.md` defines, field by field, every file the gap office's tools
read and write. Every gap tool task (BL-1721 to BL-1736) then builds against one written
contract.

## Context

The design is ADR-0433
(`Documentation/Planning/Decisions/ADR-0433-a-gap-analysis-office-measures-curl-against-upstream-curl-releases.md`).
Read it all first: its decisions 1 to 6 fix the concepts this document turns into fields.
The model to mirror is the audit office's `Audit/Instructions/Report-Format.md`,
`Audit/Findings/README.md`, `Audit/Findings/FINDING-TEMPLATE.md` and
`Audit/Scorecards/README.md`. An interactive session can read them. A dark factory lane
cannot, because the PreToolUse hook refuses `Audit/`. Everything a lane needs is therefore
restated here and in ADR-0433, and nothing in this task needs `Audit/`.

All JSON is UTF-8 without BOM. It must parse with `ConvertFrom-Json` in Windows PowerShell
5.1 and PowerShell 7, and it holds no comments. Paths inside it are repository-relative with
`/`.

`Gap-Format.md` must define each of the following, with a table of fields (name, type,
meaning) and one complete example for each:

1. **Release manifest**, `Gap/Baselines/curl-<version>.json`: `version`, `tag`
   (`curl-8_21_0`), `url`, `sha256`, `fetched` (ISO date). Also **target**,
   `Gap/Baselines/target.json`: `{ "version": "8.21.0", "decidedBy": "ADR-0433" }`.
   Also **newest**, `Gap/Baselines/newest.json`: `version`, `tag`, `published`, `checked`.
2. **Upstream inventory**, `Gap/Upstream/<version>/<area>.json`: `area`, `version`,
   `sources` (the files read), and `items[]`. Each item has `key`, `name`,
   `introducedIn` (a version string, or `null` when upstream does not say), and
   `attributes` (an area-specific object). Name the attributes each area records:
   - options: `short`, `arg`, `protocols`, `boolean`, `noForm`;
   - protocols: `scheme`, `tls`;
   - features: `name`;
   - writeout: `name`;
   - exitcodes: `number`, `curleName`, `strerror`, `exitText`;
   - environment: `name`, `kind` (`variable`, `config-path` or `config-syntax`);
   - behaviour: `number`, `sha256`, `keywords`, `tool`, `features`.
3. **The item key rule**: `<area>:<item>[:<facet>]`. It is stable across runs and versions.
   It never holds a measured value, a date or a line number. The examples are
   `options:--ech`, `options:--ech:argument`, `options:-k:alias`, `protocols:mqtts`,
   `features:HTTP3`, `writeout:time_queue`, `exitcodes:101:strerror`,
   `environment:NO_PROXY`, `behaviour:test1234`. The seven area names are exactly
   `options`, `protocols`, `features`, `writeout`, `exitcodes`, `environment` and
   `behaviour`.
4. **Area measurement**, `<run>/measurements/<area>.json`, written by each `Measure-*`
   tool: `area`, `targetVersion`, `candidateCommit`, `platform` (`windows`, `linux` or
   `macos`), `reference` (the reference curl's `--version` first line, or `null` with
   `referenceFallback: "docs"`), `measuredAt`, `items[]` with `key`, `state` (`match`,
   `gap`, `unmeasured` or `excluded`), `reason` (required for `unmeasured` and `excluded`,
   from a closed vocabulary per area that this document lists), `expected`, `actual`,
   `evidence`, `introducedIn`. Also `counts` with `match`, `gap`, `unmeasured`,
   `excluded`, `x`, `y`, where x = match and y = match + gap + unmeasured.
5. **Release diff**, `<run>/measurements/release-<new>.json`, written by
   `Compare-UpstreamReleases.ps1` (BL-1735): `fromVersion`, `toVersion`, `items[]` with
   `key`, `change` (`added`, `removed` or `changed`), `before`, `after`.
6. **Analyst report block**: the last fenced `json` block of an analyst's reply.
   `{ "analyst", "area", "run", "groups": [ { "title", "severity", "introducedIn",
   "items": [keys], "evidence", "suggestion", "touches": [Curl project folders] } ],
   "notes" }`. Every gap item in the measurement belongs to exactly one group. `touches`
   never names a `Gap/` path.
7. **Gap finding**, `Gap/Findings/GF-####-<slug>.md`, defined in
   `Gap/Findings/README.md` with `Gap/Findings/FINDING-TEMPLATE.md`:
   - front matter: `id`, `title`, `area`, `key` (the group key `<area>:<cause-slug>`),
     `severity` (`Critical`, `High`, `Medium` or `Low`, defined as in ADR-0433 decision 3),
     `status` (`open`, `closed` or `rejected`), `scope` (`target` or `newest`),
     `introduced-in`, `opened` (run stamp), `closed` (run stamp or empty), `regression`
     (`true` or `false`), `items` (list of item keys), `touches`, `task`, `tasks`;
   - sections in order: `Summary`, `Evidence`, `Suggestion`, `Measurements` (one line per
     run: stamp, how many items are still gaps), `Log`.
   Spell out the closing rule from ADR-0433 decision 5: a finding closes only when a run
   measures every item as `match`, or as `excluded` with a stated reason. It reopens with
   `regression: true` when an item measures `gap` again. Only Stewart sets `rejected`.
   There is no won't-fix status.
8. **Scorecard**, `Gap/Scorecards/<yyyy-MM-dd_HHmm>.md`, defined in
   `Gap/Scorecards/README.md`. Its fixed sections are Run (commit, platform, target,
   newest, reference), Scores (one row per area: X, Y, %, unmeasured, excluded, change
   since last run), Against the newest version, New gaps, Closed, Regressions, Unmeasured
   by reason. Also **history**, `Gap/Scorecards/history.json`: an array of
   `{ stamp, commit, platform, targetVersion, newestVersion, areas: { <area>: { x, y } },
   overall: { x, y }, newest: { x, y } }`, oldest first.
9. **Dashboard data**, `data.json`, written by `Export-GapDashboardData.ps1` (BL-1733)
   and published beside the page: `generated`, `target`, `newest`, `latest` (the last
   history entry), `history`, `open[]` (id, title, area, severity, scope, introducedIn,
   suggestion, items count, tasks, url), `closed[]` (closed in the last 5 runs),
   `regressions[]`, and `release` (`{ version, newGaps }`, or `null`).
10. **How "against the newest version" is computed**. For an inventory area: Y is the
    newest version's inventory size. X is the target-matched items whose key is neither
    removed nor changed by the release diff. Items the release diff adds count as gaps.
    For behaviour: test cases added or changed in the newest version count as unmeasured
    until a run targets that version.

`Gap/Findings/README.md` and `Gap/Scorecards/README.md` repeat only what a reader of that
folder needs and link to `Gap-Format.md` for the rest. ASCII punctuation throughout, so
PowerShell 5.1 reads the files the same as PowerShell 7.

## Acceptance criteria

- [ ] `Gap/Instructions/Gap-Format.md` has one section for each of the ten formats above. Each has a field table and a complete example, and each JSON example parses with `ConvertFrom-Json` in Windows PowerShell 5.1.
- [ ] The item key rule lists the seven area names exactly as above, with an example key per area.
- [ ] Each area's closed vocabulary of `unmeasured` and `excluded` reasons is listed. For behaviour it includes at least `needs-server:<protocol>`, `harness-unsupported`, `unknown-variable`, `libcurl-api`, `libcurl-unit-test`, `debug-build-only` and `reference-lacks:<feature>`.
- [ ] `Gap/Findings/README.md` states the closing and reopening rule of ADR-0433 decision 5 and the statuses `open`, `closed` and `rejected`. `Gap/Findings/FINDING-TEMPLATE.md` has exactly the front matter and sections listed in item 7.
- [ ] `Gap/Scorecards/README.md` lists the scorecard's fixed sections and the `history.json` entry shape.
- [ ] No file in this task names an `Audit/` path as something a gap tool reads.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
