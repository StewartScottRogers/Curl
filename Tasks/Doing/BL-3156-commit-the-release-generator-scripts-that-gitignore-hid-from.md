---
id: BL-3156
title: Commit the release generator scripts that .gitignore hid from release.yml
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.gitignore, .github/release, .github/workflows/release.yml]
requirement: none
created: 2026-10-22
completed:
---
# BL-3156 — Commit the release generator scripts that .gitignore hid from release.yml

## Goal

Every push to master rebuilds the rolling `preview` release again, with `resources.json` and `ai-help.md` attached, because the two scripts `release.yml`'s `describe` job runs are committed.

## Context

Found by the shift-end review (ADR-0710) of 76d4c7a87..2f68618ca, by the code-reviewer and silent-failure-hunter agents; Critical.

- `.github/workflows/release.yml:322` runs `./.github/release/New-ResourcesJson.ps1` and `:327` runs `./.github/release/Test-ResourcesJson.ps1`. Neither file is in git: `git check-ignore -v .github/release/New-ResourcesJson.ps1` reports `.gitignore:22:[Rr]elease/`, so BL-3147's lane wrote them but never committed them (commit fb690a3b1 has only `release.yml`, `DOWNLOAD.md` and the task file).
- The diff also made the `release` job `needs: [describe]` (`release.yml:340`), so on the next master push `describe` fails and the rolling `preview` release is never rebuilt. That breaks Stewart's standing rule that every master push refreshes the downloads, and leaves the `preview/resources.json` and `preview/ai-help.md` links in `llms.txt`, `README.md` and `DOWNLOAD.md` returning 404; `gource.yml`'s site-pages job then never publishes `download/` or `ai-help/`.
- BL-3147's task file (Done) describes the intended `resources.json` shape; BL-3151 waits on this reaching master.

## Acceptance criteria

- [ ] `.gitignore` keeps `.github/release/` tracked (a `!.github/release/` exception, or the scripts live in a folder `[Rr]elease/` does not match and `release.yml` calls them there); `git check-ignore` reports nothing for the scripts.
- [ ] `New-ResourcesJson.ps1` and `Test-ResourcesJson.ps1` are committed, PowerShell only, and write and check `resources.json` in the shape BL-3147 documents, with every entry naming a file present in `dist`.
- [ ] Running both scripts locally against a sample `dist` folder succeeds, and `Test-ResourcesJson.ps1` fails (non-zero exit) when an entry names a missing file or a wrong SHA-256.
- [ ] A `release.yml` run on a PR or master gets through the `describe` job green.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ee636d52 516954dc f55a34f8 99077a4d.
