---
id: BL-3159
title: Publish llms.txt and the MCP page from work/dark-factory only so master's hourly site-pages run stops restoring older copies
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/gource.yml, Documentation/Planning/Decisions/ADR-0763-llms-txt-and-the-agent-pages-are-published-by-a-site-pages-job.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3159 — Publish llms.txt and the MCP page from work/dark-factory only so master's hourly site-pages run stops restoring older copies

## Goal

The `gource` branch's `llms.txt` and `mcp/index.html` always come from `work/dark-factory`. Master's hourly scheduled run of the `site-pages` job no longer puts back older copies.

## Context

Found by the shift-end review on 2026-10-22 (ADR-0710). The `pr-review-toolkit:code-reviewer` reported it and it was checked against the code. Diff `6a9875312...2fc45d8fe`.

- `.github/workflows/gource.yml:456` runs `site-pages` when `github.ref` is either `refs/heads/work/dark-factory` or `refs/heads/master`.
- The job copies `llms.txt` from the repository root (line 476) and `.github/site/mcp/index.html` (line 477) out of the ref it checked out.
- It commits to `gource` whenever the file differs from what is there.
- The hourly `schedule` trigger (`cron: '17 * * * *'`, line 67) checks out `master`. Between shift-end merges, `master` holds older copies of these files than `work/dark-factory`.

So the sequence is:

1. A `work/dark-factory` push publishes the new `llms.txt`.
2. The next hourly run sees a difference and commits master's old copy back.
3. The next factory push flips it forward again.

Each flip also requests a Pages build. The comment on line 455 says the opposite of what happens: "Pushes to other branches carry older copies of the sources and would put them back", yet master is one of those older sources. BL-1206 fixed the same problem for `board-page` by publishing from `work/dark-factory` only (line 318).

Master's hourly run must still refresh `download/` and `ai-help/`. Those come from the `preview` release, not from the checkout, and it is how a new release gets picked up.

## Acceptance criteria

- [ ] On a run whose ref is not `refs/heads/work/dark-factory`, `site-pages` in `.github/workflows/gource.yml` neither copies nor commits `llms.txt` or `mcp/index.html`.
- [ ] Master's hourly run of `site-pages` still regenerates and commits `download/index.html` and `ai-help/` from the `preview` release when they change.
- [ ] The comment above `site-pages` and the header comment at the top of `gource.yml` say which branch publishes which page.
- [ ] `Documentation/Planning/Decisions/ADR-0763-llms-txt-and-the-agent-pages-are-published-by-a-site-pages-job.md` states the same branch rule.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 598cf571 dec10a34 b0d7dd2a 87f42a66.
