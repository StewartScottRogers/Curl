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
completed: 2026-10-02
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

- [x] Two tabs across the top, Tasks and Audit; the Tasks tab draws exactly what the page draws today.
- [x] `#tasks` and `#audit` in the address open that tab, and switching tabs updates the address, so either can be bookmarked. No hash opens Tasks.
- [x] The Audit tab label shows the count of `proposed` findings, e.g. `Audit (3)`, and the count is visible from the Tasks tab.
- [x] The Audit tab has four columns, in order: Waiting for you (`proposed`), Approved (`accepted`), Deferred, Blocked. `rejected` and `closed` findings are in folded lists under the board, closed by default.
- [x] Each card shows ID, title, auditor and severity; opening it shows the finding's full Markdown text, its `reason:` and its `## Log`.
- [x] A page load makes at most one more GitHub API request than today (checked in the browser's network panel; Notes records the count).
- [x] Works at phone width with no sideways page scroll, in light and dark colour schemes.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- Delivered directly in `.github/board/site/index.html` (a single page, no C#), not through the full `/feature` stages: there was no seam or library to plan.
- Tabs are `<a href="#tasks|#audit">` links; `hashchange` switches the panels, so the browser's address, back button and bookmarks all work. Any other hash (or none) opens Tasks.
- API requests: the audit findings are one more source in `sources` (the `audit` branch's recursive tree), so a load makes 3 GitHub API calls where it made 2, and the refresh interval follows to 180 s by the existing rule. Measured with Edge's net log on a live load: `trees/work%2Fdark-factory`, `contents/status.json?ref=board`, `trees/audit` on api.github.com, plus four raw.githubusercontent.com reads (AF-0001 to AF-0004), which cost no API calls.
- Choice: findings are listed from both trees the page already has - the task branch's (which carries `master`'s copies, merged in) and the `audit` branch's - with the `audit` copy winning by ID. A separate `master` tree call would have broken the one-extra-request limit, and `work/dark-factory` merges `master` regularly.
- Choice: finding texts are cached by blob SHA, so a refresh re-reads only changed findings. Raw URLs use `refs/heads/<branch>`.
- Choice: a card is a `<details>` (opens in place, works on a phone without a dialog); opened cards and folded lists stay open across refreshes. The full text is shown as wrapped plain Markdown in a `<pre>`, not rendered HTML: no Markdown renderer is needed and nothing from a finding is interpreted as markup. A finding whose text cannot be read, or whose status is unknown, goes to a third folded list "Unreadable or unknown status" rather than vanishing.
- Front matter parsed: `title`, `auditor`, `severity`, `status`, `reason` (title falls back to the `# ` heading, then the file name). This lane could not read the audit folder (audit guard), so the format was checked on the live run instead: all four real findings showed auditor and severity correctly.
- Phone width: at 700 px and narrower both boards collapse to one column. That also changes the Tasks tab on a phone, which today scrolls sideways through five 180 px columns; at desktop width the Tasks tab is unchanged. Checked in headless Edge with a 360 px iframe and every card open: scrollWidth = clientWidth = 345 on both tabs. The Tasks tab's DOM (lanes and board) is byte-identical to the old page's against the fixtures. Dark scheme checked by screenshot with `--force-dark-mode`; the page keeps `color-scheme: light dark`, and the selected tab uses `Canvas`.
- New preview overrides `?audit=`, `?findings=` and `?auditBranch=`, with fixtures `fixtures/audit-tree.json` and `fixtures/findings/` (one finding per status, one missing to exercise the unreadable list), and two finding entries added to `fixtures/tree.json` to exercise the union.
- Build clean; fast tests 24,075 passed, 0 failed, across 33 assemblies.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. The board page has a Tasks tab and a findings tab with a read-only kanban of audit findings and a count of those waiting
