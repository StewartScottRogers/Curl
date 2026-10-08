---
id: BL-1742
title: Draw the gap dashboard page at .github/gaps/site/index.html
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1733]
touches: [.github/gaps]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1742 — Draw the gap dashboard page at .github/gaps/site/index.html

## Goal

`.github/gaps/site/index.html` is a self-contained page that draws the gap analysis office's
dashboard from the `data.json` beside it. It shows Curl's equivalence with the targeted and
the newest curl, the trend over runs, and the open gaps with their suggestions. It works in
light and dark themes and at phone width.

## Context

This is ADR-0433 decision 8. The data shape is the "Dashboard data" section of
`Gap/Instructions/Gap-Format.md` (BL-1720). The fixture output of
`Export-GapDashboardData.ps1` (BL-1733), `Gap/Tools/Fixtures/dashboard/expected-data.json`,
is the preview data: copy it to `.github/gaps/fixtures/data.json`.

Match the board page, `.github/board/site/index.html`:

- one file, with no build step, framework, external script, stylesheet or font;
- its header comment lists the query overrides;
- its CSS variables (`--line`, `--card`, `--warn`, `--bad`) with `color-scheme: light dark`;
- its `nav.tabs` style;
- its `@media (max-width: 700px)` single-column layout.

Read its first 160 lines. The local preview server is `.github/gource/serve.cs`, and the
board's fixtures under `.github/board/fixtures/` show how a `?tree=` override points the page
at a fixture.

**The page.**

- **Navigation** at the top, styled like the board's tabs: `Gource` (`../`), `Board`
  (`../board/`), `Coverage` (`../coverage/`), and `Gaps` (this page, marked selected).
  BL-1744 adds the matching links to the other three pages.
- **Release banner** when `release` is set: "curl <version> released: <n> new gaps".
- **Headline**: the overall percentage and X of Y against the targeted version, and against
  the newest version beside it. Show the run's stamp, commit, platform and reference.
- **Areas**: one row per area with X, Y, %, a bar, unmeasured and excluded counts, and the
  change since the previous run.
- **Trend**: an inline SVG line chart of the overall score and each area's score over
  `history`, with no chart library. Hovering or tapping a point shows its stamp and value.
- **Open gaps**: a list with filter controls for area, severity, scope and
  `introducedIn` version. Each gap shows id, title, severity, item count, task IDs, its
  suggestion in a `<details>`, and a link to the finding (`url`).
- **Regressions** and **Recently closed**: two short lists.
- **Empty state**: with `latest: null`, show "No gap analysis has run yet" and keep the
  navigation.
- **Query override**: `?data=url` reads another data file, for example
  `?data=../fixtures/data.json`. Only same-origin URLs are allowed, as the board does for
  `?contents=`.

## Acceptance criteria

- [x] `.github/gaps/site/index.html` exists as one file with no `<script src`, `<link rel="stylesheet"` or `@import` and no external URL other than links to github.com. `.github/gaps/fixtures/data.json` is committed.
- [x] Served with `dotnet run --file .github/gource/serve.cs -- .github/gaps` and opened at `http://localhost:8000/site/?data=../fixtures/data.json`, the page shows the banner, headline, areas table, trend chart, filterable open gaps, regressions and recently closed from the fixture. Filtering by one area hides the other areas' gaps. Note in this task's Notes which browser was used.
- [x] Opened with a data file whose `latest` is `null`, it shows "No gap analysis has run yet".
- [x] At a 375-pixel-wide viewport, nothing scrolls sideways. With the system theme switched to dark, text and bars keep contrast. Both are checked and noted.
- [x] The header comment documents the page, its data source and `?data=`.

## Notes

- The page reads `./data.json` by default; `?data=` is resolved against the page and refused
  (with a message) when it is not on the page's own origin. Unlike the board's `?contents=`
  it is not limited to localhost: it sends no token, so same-origin is enough.
- Areas rows show unmeasured and excluded counts when an area object carries them
  (`unmeasured`, `excluded`); `history.json` areas today hold only `x` and `y`, so the
  fixture shows `—`. The bar sits under the percentage in the same cell so the table fits
  at phone width.
- `.github/gaps/fixtures/empty.json` (`"latest": null`) is committed beside `data.json` for
  the empty state.
- Checked in Microsoft Edge (headless, Chromium) against `serve.cs` on port 8743 with a
  throwaway harness page that framed the dashboard at 375 px: all six fixture gaps listed;
  Area = options left GF-0001, GF-0004, GF-0006 only; page scrollWidth 360 = clientWidth
  360 and the areas table's 328 = 328, so nothing scrolls sideways; `empty.json` shows
  "No gap analysis has run yet" with the four tabs. Banner, headline (95.0%, 306 of 322;
  newest 93.3%, 304 of 326), areas with +0.4 change, trend SVG (three series, six points
  with stamp and value), regressions and recently closed all drawn. A dark-theme
  screenshot (`preferredColorScheme` dark) kept text, bars and series readable.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Gap dashboard page draws data.json: banner, headline, areas, SVG trend, filterable open gaps, regressions, closed, empty state, ?data= override
