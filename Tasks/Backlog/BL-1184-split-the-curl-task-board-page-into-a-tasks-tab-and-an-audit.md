---
id: BL-1184
title: Split the Curl task board page into a Tasks tab and an Audit findings kanban tab
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1183]
touches: [.github/board]
requirement: none
created: 2026-10-02
completed:
---
# BL-1184 — Split the Curl task board page into a Tasks tab and an Audit findings kanban tab

## Goal

The Curl task board page has two tabs: **Tasks**, the page as it is today, and **Audit (n)**, a read-only kanban of the audit office's findings, with n the findings waiting for Stewart.

## Context

- Stewart, 2026-10-02, chose tabs over appending findings to the bottom of the page: findings have other states, come from another branch, and are for deciding, not watching.
- Page: `.github/board/site/index.html` (ADR-0129). It reads `work/dark-factory`'s tree and the `board` branch's `status.json` from the GitHub REST API, unauthenticated (60 requests an hour per viewer).
- Findings: `Audit/Findings/AF-*.md` on the `audit` branch (and on `master` once merged; show the `audit` branch's copy when both exist). Statuses and their front matter come from BL-1183: `proposed`, `accepted`, `deferred`, `blocked`, `rejected`, `closed`, plus `reason:` and a `## Log`.
- Read each finding's text through `raw.githubusercontent.com` (no API rate limit, CORS allowed), listing them from one tree call, so a page load costs at most one extra API request.
- This task only reads. The buttons that change a finding are BL-1185.
- The page is not an audit path, so a lane may do this; it must not edit anything under `Audit/`.

## Acceptance criteria

- [ ] Two tabs across the top, Tasks and Audit; the Tasks tab draws exactly what the page draws today.
- [ ] `#tasks` and `#audit` in the address open that tab, and switching tabs updates the address, so either can be bookmarked. No hash opens Tasks.
- [ ] The Audit tab label shows the count of `proposed` findings, e.g. `Audit (3)`, and the count is visible from the Tasks tab.
- [ ] The Audit tab has four columns, in order: Waiting for you (`proposed`), Approved (`accepted`), Deferred, Blocked. `rejected` and `closed` findings are in folded lists under the board, closed by default.
- [ ] Each card shows ID, title, auditor and severity; opening it shows the finding's full Markdown text, its `reason:` and its `## Log`.
- [ ] A page load makes at most one more GitHub API request than today (checked in the browser's network panel; Notes records the count).
- [ ] Works at phone width with no sideways page scroll, in light and dark colour schemes.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-02: Created.
