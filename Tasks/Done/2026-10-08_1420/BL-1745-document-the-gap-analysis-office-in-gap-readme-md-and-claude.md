---
id: BL-1745
title: Document the gap analysis office in Gap/README.md and CLAUDE.md and the glossary
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1741, BL-1736, BL-1743, BL-1744]
touches: [Gap/README.md, CLAUDE.md, Documentation/Wiki/Glossary.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1745 — Document the gap analysis office in Gap/README.md and CLAUDE.md and the glossary

## Goal

A reader of `Gap/README.md`, root `CLAUDE.md`, the glossary or the product overview learns
what the gap analysis office is, how to run it, where its results are, and how gaps become
tasks and close. Every statement must be true of the office as built.

## Context

The design is ADR-0433. By the time this task starts, BL-1719 to BL-1744 have built the
office. Read what exists under `Gap/`, `.claude/agents/gap-*.md`,
`.github/workflows/gap-release-watch.yml`, the `gaps-page` job in `.github/workflows/gource.yml`
and `.github/gaps/site/index.html`. Write what is there, not what the tasks said. Where
something differs from ADR-0433, describe the code and file a follow-up task; do not edit
the ADR.

**`Gap/README.md`**: rewrite BL-1719's stub in the shape of `Audit/README.md`.

- A paragraph saying what the office measures and against what (the targeted version's
  release data, ADR-0433).
- The folder map, with every row's "Built by" now naming the task that built it.
- **Running it**: `Gap\RunGapAnalysis.cmd -NewTab`, its parameters, and where the run
  folder is.
- **Scores**: X of Y, the four item states, and why unmeasured counts against the score.
- **From gap to task**: a summary of `Gap/Triage.md`, with a link to it.
- **Closing and regressions.**
- **The moving baseline**: the weekly watcher, `scope: newest`, and retargeting by ADR.
- **The dashboard**: its URL, `https://stewartscottrogers.github.io/Curl/gaps/`.
- **Independence**: a note that BL-1746 makes `Gap/` an audit path. State it as intent
  until BL-1746 is Done.

**Root `CLAUDE.md`**: add a `## Gap analysis office` section after `## Audit office`. Keep
it as short as that section's opening:

- the office measures Curl against upstream curl's release data in seven areas;
- the analysts and their models;
- `Gap\RunGapAnalysis.cmd -NewTab` in herdr, never `Start-Process`;
- findings `Gap/Findings/GF-####-*.md` on the `gap` branch, closed only on re-measurement;
- Claude files their tasks without asking, and Stewart may reject any;
- the weekly release watcher, and that retargeting is an ADR;
- the dashboard URL.

In the Git and GitHub section, add a line saying that the `gap` branch's pull requests are
merged under the same standing exception as the audit office's.

**`Documentation/Wiki/Glossary.md`**: add a `## The gap analysis office` section with these
terms: targeted version, newest version, upstream inventory, item, item key, measurement,
match / gap / unmeasured / excluded, gap finding, scope, regression, release diff, analyst,
reference build. Each says exactly what the code uses it for (CLAUDE.md, "Say what it does,
do what it says").

**`Documentation/Product/Product-Overview.md`**: in its Success criteria section (around
"1. **Upstream conformance.**"), add a sentence saying the gap analysis office measures this
continuously and where its dashboard is.

## Acceptance criteria

- [x] `Gap/README.md` has the sections listed above, with no row marked planned for a part that exists.
- [x] Root `CLAUDE.md` has a `## Gap analysis office` section with the seven points above, and the Git and GitHub section names the `gap` branch's pull requests.
- [x] `Documentation/Wiki/Glossary.md` has a `## The gap analysis office` section defining every term listed.
- [x] `Documentation/Product/Product-Overview.md`'s Success criteria mention the office and its dashboard URL.
- [x] Every command and path named in these documents exists in the repository. Spot-check by running `Gap\RunGapAnalysis.cmd -DryRun` and listing each named file.

## Notes

- Delivered directly in the session rather than through align-and-document, to stay inside the run's cost cap; every statement was checked against the scripts' help, `Gap/Instructions/Gap-Format.md`, `Gap/Triage.md`, the agents' `model:` lines, `gap-release-watch.yml` and the dashboard page.
- No difference from ADR-0433 found that needed a follow-up task: the analysts' models, the score rule, the closing rule and the watcher's schedule all match it.
- `Gap\RunGapAnalysis.cmd -DryRun` refuses inside a lane (CURL_DARK_FACTORY_LANE set), as documented; run with the variable unset it printed all eleven steps' commands. Every path named in the four documents exists.
- The Independence section states BL-1746 as intent, since BL-1746 is still in Backlog.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Gap/README.md, CLAUDE.md, the glossary and the product overview now document the gap analysis office as built
