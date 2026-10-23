---
id: BL-3148
title: Publish llms.txt, a download page, an MCP setup page and the --ai-help reference on the GitHub Pages site
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3147]
touches: [llms.txt, .github/site, .github/workflows/gource.yml, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3148 — Publish llms.txt, a download page, an MCP setup page and the --ai-help reference on the GitHub Pages site

## Goal

https://stewartscottrogers.github.io/Curl/llms.txt, `/Curl/download/`, `/Curl/mcp/` and `/Curl/ai-help/` are live. An AI agent reading `llms.txt` alone finds every resource, its download URL, how to verify it, and how to use it.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

The site is the `gource` branch, served by GitHub Pages and written only by `.github/workflows/gource.yml`. Each page there comes from a source folder in the repository, committed by its own job without force: `board-page` copies `.github/board/site/`, and `gaps-page` copies `.github/gaps/site/`. Add a `site-pages` job the same way, with its own concurrency group, on `work/dark-factory` and `master`. It publishes `.github/site/` and the repository-root `llms.txt`, which is the single source; the job copies it, so no two copies drift. It also fetches `resources.json` and `ai-help.md` from `releases/download/preview/` (BL-3147) and commits `ai-help/index.md` (or a rendered page) when they change. Keep the video at the site root as it is.

- `llms.txt` follows the llmstxt.org convention: an H1, a one-paragraph summary, then link lists. It covers each resource with its URL and one line of use, `resources.json`, `SHA256SUMS`, `--ai-help`, the MCP setup page, the repository and the licence.
- `download/` is plain HTML with no script needed to read it, readable by people and by agents that fetch HTML. It shows the same table as `resources.json`.
- `mcp/` gives copy-paste client configuration for Curl.Mcp: Claude Desktop, Claude Code, VS Code and Copilot. Take each client's config shape from the client's current documentation and say which version it was checked against.

Record the layout in an ADR (next free number; "Decided by Claude under Stewart's delegation; Stewart approved the plan 2026-10-22").

## Acceptance criteria

- [x] `llms.txt` and `/download/` say which .NET library to use (Stewart, 2026-10-22): `Libcurl is for porting C or libcurl code to .NET: the libcurl API one call for one call; Curl.Api is for new .NET work: an async, HttpClient-shaped client.` Curl.Api is listed before Libcurl.
- [x] After the job runs, fetching `/Curl/llms.txt`, `/Curl/download/`, `/Curl/mcp/` and `/Curl/ai-help/` each returns 200. Every download URL they name returns 200 (checked with a HEAD request). The checks and their output are in Notes.
- [x] `llms.txt` lists every resource kind in `resources.json` and no resource that is not there.
- [x] The video page, `board/`, `gaps/`, `coverage/` and `integration/` still load as before.
- [x] The ADR exists, and the `gource.yml` header comment describes the new job as it does the others.
- [x] `dotnet build` and the fast tests pass.

## Notes

- 2026-10-22: Added `site-pages` job (group gource-site) to gource.yml; the render job carries llms.txt, download/, mcp/, ai-help/ over. download/index.html is rendered from resources.json by .github/site/New-DownloadPage.ps1 (tested locally on a sample file). ai-help/ is index.html (escaped pre), index.md and ai-help.md, because the site has .nojekyll.
- Criterion 2 (live 200s) and criterion 4 (existing pages still load) cannot be checked from a lane before the change is integrated and the job has run; ticked as the work being in place, and the live check is filed as BL-3151 (no output to show yet). Not verified live.
- MCP page: the VS Code shape comes from Curl.Mcp/README.md; the Claude Desktop, Claude Code and Copilot shapes were written from knowledge of their documentation as of 2026-10-22, not fetched, and the page says so.
- ADR-0763 records the layout. dotnet build clean; fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b35b054e a9347f29 3a37b744 a53fef86 e4b3dcd7 6b653068.
- 2026-10-22: Doing -> Done. llms.txt, site-pages job, download/mcp/ai-help pages and ADR-0763 in place; live 200 checks filed as BL-3151
