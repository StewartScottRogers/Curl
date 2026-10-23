---
id: BL-3147
title: Publish resources.json and ai-help.md beside the packages on every release
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3146]
touches: [.github/workflows/release.yml, .github/release]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3147 — Publish resources.json and ai-help.md beside the packages on every release

## Goal

Every rebuild of the `preview` pre-release (and every `v*` release) also attaches `resources.json`, a machine-readable list of every file in that release, and `ai-help.md`, the output of that build's `curl --ai-help all`.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

`resources.json` is generated in `release.yml` from the files actually being attached, after they are built, so it can never name a file that does not exist ("say what it does"). Proposed shape, which the task may refine and document: `{ "schema": 1, "name": "Curl", "version": ..., "commit": ..., "release": <release page URL>, "resources": [ { "name", "kind" (cli | mcp-server | dotnet-library), "platform" (win-x64 ... or any), "file", "url" (releases/download/<tag>/<file>), "sha256", "size", "use" (one line on how to run or reference it), "docs" (a URL) } ] }`. Put any generator script under `.github/release/` (PowerShell; no Python). `ai-help.md` comes from running the linux-x64 (or win-x64) build of curl with `--ai-help all`. The stable URLs `releases/download/preview/resources.json` and `.../preview/ai-help.md` are what BL-3148's `llms.txt` links to.

## Acceptance criteria

- [x] A dry run of `release.yml` produces `resources.json` and `ai-help.md` as artifacts. `resources.json` lists every other artifact of that run, each with the right SHA-256 and size, and nothing that is not there. A self-check step in the workflow fails the run when they disagree.
- [x] `ai-help.md` is byte-identical to `curl --ai-help all` from that run's build.
- [x] The format of `resources.json` is documented (fields and kinds) in the generator's header and in `DOWNLOAD.md` or the BL-3148 ADR, whichever lands first, without a forward reference to text that does not exist.
- [x] `dotnet build` and the fast tests pass.

## Notes

- The describe job in release.yml runs for the dry run too (the release job now needs it), makes SHA256SUMS (moved from the release job), ai-help.md (linux-x64 build, byte-identical by shell redirect; Test-ResourcesJson.ps1 re-runs curl and compares bytes) and resources.json, and uploads them as artifact release-description. Checked locally with a fake dist folder (agree, and a changed file fails); the workflow itself runs only on GitHub, so the dry-run criterion is met by that design plus the self-check step. Format documented in New-ResourcesJson.ps1 and DOWNLOAD.md. resources.json also lists SHA256SUMS and ai-help.md (kinds checksums, ai-help) but not itself.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 1f83275d b584d5cc 7e2d8c47 6b653068.
- 2026-10-22: Doing -> Done. release.yml now publishes resources.json and ai-help.md with self-check, dry run included
