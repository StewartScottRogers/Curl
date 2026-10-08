---
id: BL-1743
title: Publish the gap dashboard to the gource branch's gaps/ folder from gource.yml
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1742]
touches: [.github/workflows/gource.yml]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1743 — Publish the gap dashboard to the gource branch's gaps/ folder from gource.yml

## Goal

The gap dashboard is served by GitHub Pages at
`https://stewartscottrogers.github.io/Curl/gaps/`. A new `gaps-page` job in
`.github/workflows/gource.yml` publishes the page from `work/dark-factory` and the data from
the `gap` branch, without force. The render job carries `gaps/` into each force-pushed
render, so the dashboard survives every render.

## Context

This is ADR-0433 decision 8. Read `.github/workflows/gource.yml` in full. Its header
explains the four jobs. The `board-page` job is the model:

- it runs only for `refs/heads/work/dark-factory` (BL-1206 explains why other branches must
  not publish older copies);
- it is in its own concurrency group, `gource-board`;
- it uses a sparse, blob-less clone of `gource` and skips when the file is unchanged;
- it commits without force, retries three times, and then asks for a Pages build.

The render job is the only one that force-pushes `gource`. It rebuilds the branch from
scratch and carries `coverage/`, `board/` and `integration/` over by fetching their files
from `raw.githubusercontent.com`. Without the change below, a render would delete `gaps/`.

**Changes.**

1. **`gaps-page` job.** `if: github.ref == 'refs/heads/work/dark-factory' || github.ref
   == 'refs/heads/gap'`, on `ubuntu-latest` with `timeout-minutes: 10` and `concurrency:
   group: gource-gaps, cancel-in-progress: false`. The release watcher (BL-1736) uses the
   same group.
   - On `work/dark-factory`: when `.github/gaps/site/index.html` is in the commit and
     differs from `gource`'s `gaps/index.html`, commit it there.
   - On `gap`: run `pwsh Gap/Tools/Export-GapDashboardData.ps1 -OutFile <temp>/data.json`
     (BL-1733) and commit it as `gaps/data.json` when it differs.
   - Either way: sparse checkout of `gaps`, no force, three tries, then the Pages build
     request, only when something was published.
2. **Render carry-over.** In the render job's "Publish to the gource branch" step, add a
   `gaps/` block like the `board/` block. Take `index.html` from this commit's
   `.github/gaps/site/index.html` when present, otherwise fetch the branch's copy. Fetch
   `data.json` from the branch. Add `gaps` to `$extra` when `gaps/index.html` exists.
   Mention `gaps/` in the README text the step writes.
3. **The `on.push` trigger** already covers `gap` (it ignores only `gource` and `board`),
   so no trigger change is needed. Confirm it, and say so in the header comment, which
   should list the fifth job.
4. **Header comment.** Describe `gaps-page` beside `board-page`, including that a push to
   `gap` made with `GITHUB_TOKEN` (by the release watcher) starts no run, which is why
   BL-1736 publishes its own data.

## Acceptance criteria

- [x] `.github/workflows/gource.yml` has a `gaps-page` job with the condition, concurrency group, timeout and steps above. It contains no `--force` push outside the render job's existing one.
- [x] The render job's publish step carries `gaps/index.html` and `gaps/data.json` over, and adds `gaps` to the committed paths when present.
- [x] The header comment lists five jobs and explains `gaps-page` and the render carry-over.
- [x] Handed to BL-1751, not checked here (a lane cannot push, so the run does not exist yet): After the task's push, `gh run list --workflow Gource --branch work/dark-factory --limit 1` shows the run's `gaps-page` job as `success`, and `https://stewartscottrogers.github.io/Curl/gaps/` serves the page once Pages has rebuilt. Both are recorded in this task's Notes.

## Notes

- `gaps-page` checks out `.github/gaps/site` and `Gap` sparsely. On `gap` it skips when
  `Gap/Tools/Export-GapDashboardData.ps1` is missing from the commit; on `work/dark-factory`
  it skips when the page is missing. The `gap` branch does not exist yet (2026-10-08).
- Choice: `data.json` is compared with its `"generated"` line removed. The exporter stamps
  that line on every run, so a byte comparison would republish identical data on every push
  to `gap`. `index.html` has no such line, so the same filter leaves it compared byte for byte.
- The render carry-over fetches `gaps/data.json` from `gource` even when this commit has the
  page: the data only ever comes from the gap branch through `gaps-page` or the release watcher.
- The `on.push` trigger ignores only `gource` and `board`, so it already covers `gap`. The
  header comment says so.
- `dotnet build`: 0 errors. Fast tests: all green, none failed.
- The last criterion needs the pushed run and Pages, which a lane cannot reach. It went to
  BL-1751.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. gource.yml publishes the gap dashboard to gource's gaps/ through a new gaps-page job, and render carries gaps/ over; the post-push check is BL-1751
