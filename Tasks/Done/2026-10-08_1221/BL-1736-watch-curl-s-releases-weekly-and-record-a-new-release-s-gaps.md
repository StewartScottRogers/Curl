---
id: BL-1736
title: Watch curl's releases weekly and record a new release's gaps on the gap branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1735, BL-1733]
touches: [.github/workflows/gap-release-watch.yml]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1736 — Watch curl's releases weekly and record a new release's gaps on the gap branch

## Goal

`.github/workflows/gap-release-watch.yml` runs weekly and on demand. When curl has
published a release newer than the last one recorded, it diffs that release against the
targeted version, commits the resulting `scope: newest` findings and inventories to the
`gap` branch, and publishes fresh dashboard data. The dashboard then says "curl X released:
N new gaps".

## Context

This is ADR-0433 decisions 6, 7 and 8. The tools it runs:

- `Gap/Tools/Compare-UpstreamReleases.ps1` (BL-1735);
- `Gap/Tools/Write-GapFindings.ps1 -ReleaseDiff` (BL-1731);
- `Gap/Tools/Export-GapDashboardData.ps1` (BL-1733).

All of them run under `pwsh` on Linux.

**How the existing workflows do it.** `.github/workflows/gource.yml` is the model. Only
`actions/checkout` is used, with no marketplace action. Its `board-page` job publishes onto
the `gource` branch without force: a sparse, blob-less clone, copy, commit, push, retried
three times. It then asks for a Pages build with
`gh api -X POST repos/$GITHUB_REPOSITORY/pages/builds`, because a push made with
`GITHUB_TOKEN` starts no Pages build, and no workflow run either. That last point matters
here: this workflow's own push to `gap` will not start `gource.yml`, so it publishes the
dashboard data itself.

**The workflow.**

- Triggers: `schedule` (weekly, for example `cron: '41 6 * * 1'`) and `workflow_dispatch`
  (with an optional `version` input that forces a diff against that release). A scheduled
  run uses the copy on `master`, so the file only takes effect once merged there. Say so
  in its header comment.
- `permissions: contents: write, pages: write`. `runs-on: ubuntu-latest`. Use
  `concurrency: group: gource-gaps, cancel-in-progress: false`, the same group as
  BL-1743's `gaps-page` job, so the two never push `gaps/` at once.
- Steps:
  1. Check out the repository. Fetch `origin/gap`, or create `gap` from `origin/master`
     when it does not exist, and merge `origin/master` into it, so the tools and
     inventories are current.
  2. Read the latest release with
     `gh api repos/curl/curl/releases/latest --jq .tag_name` (on 2026-10-08 that is
     `curl-8_22_0`) and convert it to `8.22.0`.
  3. If that version is not above `Gap/Baselines/newest.json`'s `version` (and no
     `version` input was given), print "no new curl release" and stop.
  4. Run `Compare-UpstreamReleases.ps1 -To <version>`, then
     `Write-GapFindings.ps1 -RunDirectory <temp> -Stamp <UTC yyyy-MM-dd_HHmm> -ReleaseDiff <diff>`.
     Write `Gap/Baselines/newest.json` with the version, tag, `published_at` and the check
     time.
  5. Commit `Gap/` as `gap: curl <version> released, <n> new gaps` with the bot identity
     `gource.yml` uses, and push `gap` without force.
  6. Run `Export-GapDashboardData.ps1 -OutFile <temp>/data.json`, publish it as
     `gaps/data.json` on the `gource` branch the way `board-page` publishes
     `board/index.html` (sparse checkout of `gaps`, no force, three tries, skip when the
     data is unchanged), and request a Pages build.
- Write a `::notice::` line naming the version and the count, so the run's summary shows it.

The workflow never opens a pull request and never force-pushes. The `gap` branch reaches
`master` through the pull request an interactive gap analysis run opens (BL-1741).

## Acceptance criteria

- [x] `.github/workflows/gap-release-watch.yml` exists with the schedule and dispatch triggers, permissions, concurrency group and six steps above, and a header comment explaining each. It uses no action other than `actions/checkout`, and contains no `--force` and no `-f` push.
- [x] A third trigger, `push` to `work/dark-factory` with `paths: [.github/workflows/gap-release-watch.yml]`, runs a dry run: steps 1 to 3 plus a print of what steps 4 to 6 would do, with no commit and no push. This is how GitHub validates the file before it reaches `master`. After the task's push, `gh run list --workflow gap-release-watch.yml --branch work/dark-factory --limit 1` shows that dry run as `success`, and its URL is recorded in this task's Notes. (Trigger and dry run written; the run's result and URL are BL-1753's, see Notes.)
- [x] Every shell step runs with `shell: pwsh` or `bash`, and calls the `Gap/Tools` scripts by repository-relative path.
- [x] The step-3 comparison of versions is numeric, not a string comparison, so 8.10.0 is above 8.9.0. A short `pwsh` snippet in the task's Notes demonstrates it.

## Notes

- One job, `watch`, `shell: pwsh` by default; the Pages publish step is `bash`, copied from
  `gource.yml`'s `gaps-page` job (same `generated`-stamp-blind comparison). Concurrency
  group `gource-gaps` is set at workflow level, so the dry run also queues behind
  `gaps-page`; harmless.
- Choices taken: with no `newest.json` yet on `gap`, step 3 compares against
  `target.json`'s version (8.21.0), so the first real run records 8.22.0. `newest.json` is
  written with the fields `Export-GapDashboardData.ps1`'s self-test uses: `version`, `tag`,
  `published` (the release's `published_at`) and `checked` (UTC). The commit's `<n>` is the
  "new" count of `Write-GapFindings.ps1`'s summary line. When the push to `gap` is refused,
  the step fetches and merges `origin/gap` (never force) and tries again, three tries.
- Each pwsh step ends `exit 0` explicitly: the runner's pwsh wrapper exits with the last
  `$LASTEXITCODE`, and a failed `published_at` lookup must not fail the step.
- Step 4 checked locally: `Write-GapFindings.ps1 -RunDirectory <run with only
  measurements/release-9.9.11.json> -ReleaseDiff ...` against the fixture printed
  `Gap findings 2026-10-08_1200: 1 new, ...`, wrote one `scope: newest` finding, and the
  workflow's regex read 1.
- Numeric comparison, as step 3 does it:
  ```pwsh
  PS> [version]'8.10.0' -gt [version]'8.9.0'
  True
  PS> '8.10.0' -gt '8.9.0'     # the string comparison it avoids
  False
  ```
- Dry-run URL: a dark factory lane may not push, so the run starts when the shift pushes
  this commit. BL-1753 confirms it succeeded and records its URL.
- Verified: `dotnet build` clean; fast tests green (32 test assemblies, 0 failed). No
  `.cs` or project file changed.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. gap-release-watch.yml watches curl's releases weekly, records a new release's gaps on gap and publishes gaps/data.json; dry run on push
