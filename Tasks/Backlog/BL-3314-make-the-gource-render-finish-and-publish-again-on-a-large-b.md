---
id: BL-3314
title: Make the Gource render finish and publish again on a large, busy repository
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/gource.yml, .github/gource/render.sh, .github/gource/make-log.cs, .github/gource/make-master-playlist.cs, .github/gource/site/index.html]
requirement: none
created: 2026-10-23
completed:
---
# BL-3314 — Make the Gource render finish and publish again on a large, busy repository

## Goal

The Gource video on GitHub Pages renders and publishes again at least every few hours while the dark factory is busy, and the site stays well under GitHub's limits as the repository grows.

## Context

Stewart approved this plan on 2026-10-23 ("approved", to the interactive session's proposal). The `showcase-publisher` agent owns this workflow; read `.github/workflows/gource.yml`'s header and `.github/gource/render.sh` first.

Measured on 2026-10-23: the last published render is from 2026-10-22 17:26 Arizona (`rendered-at.txt` on `origin/gource`). The repository has 14,431 tracked files (3,265 of them archived tasks under `Tasks/Done/<stamp>/`) and 11,768 commits. A render takes about 44 minutes after a coverage job of up to 60. The last render's site was 714,878,668 bytes (Pages limit 1 GB) and `gource.mp4` 85,979,379 bytes (the step stops at 95 MiB).

Two causes:

1. **Renders are cancelled.** `decide`, `coverage` and `render` share the concurrency group `gource-render`. A group holds one running and one pending job, and a newer pending job replaces an older one. While coverage runs, the factory's pushes queue `decide` jobs, and when `render` queues, a later run's `decide` replaces it. Of the last 200 runs, 140 were cancelled; a sample of 40 showed 2 renders cancelled after their coverage succeeded (e.g. `decide=success coverage=success render=cancelled`).
2. **The one finished render lost its push.** Run 39954358913 rendered for 44 minutes, then its force push failed with `! [remote rejected] gource -> gource (cannot lock ref 'refs/heads/gource': is at 9e816ca... but expected 1205503...)`, because `board-page`, `site-pages` or `integration.yml` committed to the `gource` branch during the roughly 60-second upload.

The approved changes:

1. `decide` runs outside any concurrency group. `render` keeps its own group (`gource-render`, `cancel-in-progress: false`), so a newer render can only replace an older pending render and the newest state is always rendered. `coverage` gets its own group, and `render` no longer waits on it: it already copies whatever coverage files are on the branch.
2. The publish step retries its push up to 3 times. Before each retry it re-fetches the carried-over files (coverage/, board/, gaps/, integration/, llms.txt, download/, mcp/, ai-help/) so a page committed meanwhile is kept, and it rebuilds the single commit.
3. Cadence: render at most every 3 hours (`age >= 10800` for a push or the hourly schedule), plus on every push to `master` (the shift-end merge) when there are new commits, plus at least once a day, plus `workflow_dispatch`.
4. Size: drop the 4K H.264 rung. `hls/h264/` keeps only 1080p, and `make-master-playlist.cs` and the viewer page (`site/index.html`) follow. `gource.mp4` becomes the 1080p H.264 file (remuxed, so no second encode), and the viewer's download button says 1080p. 4K stays in AV1 only; never 8K (the 4K rung is the top).
5. Hide the archived tasks: `make-log.cs` leaves out every path under `Tasks/Done/<stamp>/` (the timestamped archive folders), but keeps `Tasks/Backlog`, `Doing`, `Done` and the rest. Stewart chose this.

Update gource.yml's header comment and render.sh's header so they say what the workflow now does (CLAUDE.md: say what it does).

## Acceptance criteria

- [ ] In `gource.yml`, `decide` has no `concurrency`, `coverage` has its own group, and `render` has `needs: decide` only, with its own `gource-render` group and `cancel-in-progress: false`.
- [ ] The publish step retries the force push up to 3 times, re-fetching the carried-over files before each retry, and fails with an `::error::` naming the last rejection after the third.
- [ ] `decide` renders on `workflow_dispatch`; when there is no previous fingerprint or the last render is at least 86400 s old; on a push to `master` with a changed fingerprint; and otherwise only when the fingerprint changed and the last render is at least 10800 s old. Its log line shows which rule applied.
- [ ] `render.sh` encodes AV1 at 2160p and 1080p and H.264 at 1080p only. `gource.mp4` is the 1080p H.264 remux. The master playlists and `site/index.html` reference only rungs that exist, and the download button is labelled 1080p.
- [ ] `make-log.cs` leaves out paths under `Tasks/Done/<timestamp>/`. A check in Notes, `dotnet run --file .github/gource/make-log.cs` on this repository, shows no such path while `Tasks/Backlog/` paths remain.
- [ ] A `workflow_dispatch` run of `gource.yml` on `work/dark-factory` after the change publishes. Notes record: the run's render job succeeds, the new `rendered-at.txt`, the site size (expected about 450 MB), the size of `gource.mp4`, and that `https://stewartscottrogers.github.io/Curl/` plays. If the lane cannot dispatch or wait for it, leave this unticked with the run link and set `lane: no` for an interactive session to finish.
- [ ] The header comments of `gource.yml` and `render.sh` describe the new jobs, cadence, rungs and log filter.

## Notes
## Log

- 2026-10-23: Created.
