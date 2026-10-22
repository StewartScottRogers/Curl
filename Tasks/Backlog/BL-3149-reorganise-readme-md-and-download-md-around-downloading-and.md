---
id: BL-3149
title: Reorganise README.md and DOWNLOAD.md around downloading and using each resource, with a For AI agents section
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3146, BL-3148]
touches: [README.md, DOWNLOAD.md, Curl.Api/README.md, Libcurl/README.md, Curl.Api/Curl.Api.csproj, Libcurl/Libcurl.csproj]
requirement: none
created: 2026-10-22
completed:
---
# BL-3149 — Reorganise README.md and DOWNLOAD.md around downloading and using each resource, with a For AI agents section

## Goal

`README.md` opens with what Curl is, then a "Download and use" table (one row per resource: curl.exe and its platforms, Curl.Mcp, Curl.Api, Libcurl), then a "For AI agents" section. The dark-factory, video, coverage and board material follows, unchanged in substance.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

The "For AI agents" section links to `llms.txt`, `resources.json`, `--ai-help` (a short example) and the MCP setup page. `DOWNLOAD.md` gains install and use sections for Curl.Api and Libcurl (from the release `.nupkg` files, BL-3146) beside the existing Curl.Mcp section, and a note on verifying files with `SHA256SUMS`. Keep the existing URLs and anchors that `install.sh`, `install.ps1` or other pages link to. Check them with a search before renaming a heading. Every statement must be true of the release and site as they are when this lands; link only to what BL-3146 and BL-3148 actually published.

## Acceptance criteria

- [ ] Wherever Curl.Api and Libcurl are offered, it says which to use (Stewart, 2026-10-22): `Libcurl is for porting C or libcurl code to .NET: the libcurl API one call for one call; Curl.Api is for new .NET work: an async, HttpClient-shaped client.` That is the README's Download and use table, a which-to-use table in the README (writing new .NET code: Curl.Api; porting C or libcurl code, or needing a CURLOPT that Curl.Api lacks: Libcurl; running commands or scripts: curl.exe; an AI agent: Curl.Mcp or `curl --ai-help`), DOWNLOAD.md's two sections, the first paragraph of `Curl.Api/README.md` and `Libcurl/README.md`, and each package's `<Description>`. Curl.Api is listed first.
- [ ] `README.md`'s first three sections are: what Curl is, Download and use, For AI agents. Every link in them returns 200.
- [ ] `DOWNLOAD.md` has Curl.Api and Libcurl sections, each with a command that was run and that worked, with output in Notes, and a SHA256SUMS verification step for each platform.
- [ ] No heading that another file links to was renamed without updating that link. A search for links to `README.md#` and `DOWNLOAD.md#` across the repository finds none broken.
- [ ] `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-22: Created.
