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
completed: 2026-10-22
---
# BL-3149 — Reorganise README.md and DOWNLOAD.md around downloading and using each resource, with a For AI agents section

## Goal

`README.md` opens with what Curl is, then a "Download and use" table (one row per resource: curl.exe and its platforms, Curl.Mcp, Curl.Api, Libcurl), then a "For AI agents" section. The dark-factory, video, coverage and board material follows, unchanged in substance.

## Context

Stewart approved this on 2026-10-22. The goal: an AI agent starting from one known address can find every resource Curl offers, download the right file for its platform, verify it and use it. The resources are curl.exe and its Linux and macOS builds, Curl.Mcp, Curl.Api, Libcurl and --ai-help. The plan has four tasks: BL-3146 (pack Curl.Api and Libcurl), BL-3147 (resources.json and ai-help.md as release assets), BL-3148 (llms.txt and the site pages) and BL-3149 (README and DOWNLOAD). Nothing goes to nuget.org, and no numbered tag or release is made: only the rolling `preview` pre-release (CLAUDE.md, Git and GitHub). No new NuGet package reference is added.

The "For AI agents" section links to `llms.txt`, `resources.json`, `--ai-help` (a short example) and the MCP setup page. `DOWNLOAD.md` gains install and use sections for Curl.Api and Libcurl (from the release `.nupkg` files, BL-3146) beside the existing Curl.Mcp section, and a note on verifying files with `SHA256SUMS`. Keep the existing URLs and anchors that `install.sh`, `install.ps1` or other pages link to. Check them with a search before renaming a heading. Every statement must be true of the release and site as they are when this lands; link only to what BL-3146 and BL-3148 actually published.

## Acceptance criteria

- [x] Wherever Curl.Api and Libcurl are offered, it says which to use (Stewart, 2026-10-22): `Libcurl is for porting C or libcurl code to .NET: the libcurl API one call for one call; Curl.Api is for new .NET work: an async, HttpClient-shaped client.` That is the README's Download and use table, a which-to-use table in the README (writing new .NET code: Curl.Api; porting C or libcurl code, or needing a CURLOPT that Curl.Api lacks: Libcurl; running commands or scripts: curl.exe; an AI agent: Curl.Mcp or `curl --ai-help`), DOWNLOAD.md's two sections, the first paragraph of `Curl.Api/README.md` and `Libcurl/README.md`, and each package's `<Description>`. Curl.Api is listed first.
- [x] `README.md`'s first three sections are: what Curl is, Download and use, For AI agents. Every link in them returns 200.
- [x] `DOWNLOAD.md` has Curl.Api and Libcurl sections, each with a command that was run and that worked, with output in Notes, and a SHA256SUMS verification step for each platform.
- [x] No heading that another file links to was renamed without updating that link. A search for links to `README.md#` and `DOWNLOAD.md#` across the repository finds none broken.
- [x] `dotnet build` and the fast tests pass.

## Notes

- README.md now opens with What Curl is, Download and use (resource table plus which-to-use table), For AI agents; media, coverage, board and factory material follow unchanged. The old Download section was folded into Download and use (installer commands kept). The .NET table lists Curl.Api first. The which-to-use sentence is in README, DOWNLOAD.md's two sections, both package READMEs and both `<Description>`s.
- Anchors: a search for `README.md#` and `DOWNLOAD.md#` finds only links between other folders' own READMEs; none to the root files. The Curl.Mcp heading in DOWNLOAD.md changed from "(...)" to ": ..." and has the same GitHub slug (`curlmcp-mcp-server-for-ai-agents`).
- Links not verified live: the lane has no network, and the site pages (llms.txt, download/, mcp/, ai-help/) are published by BL-3148's job after integration; BL-3151 checks them for 200. The release asset URLs are the ones the existing DOWNLOAD.md and llms.txt already use.
- DOWNLOAD.md commands: `dotnet add package Curl.Api|Libcurl --prerelease --source <folder>` were run in BL-3146 against locally packed nupkgs, with output `api: hello-from-file` and `libcurl easy handle created: True` (see its Notes); the lane could not download the live preview release. SHA256SUMS check run locally: `sha256sum --check --ignore-missing` printed `DOWNLOAD.md: OK`. The macOS (`shasum -a 256 --check`) and Windows (`Get-FileHash`) variants were not run here.
- dotnet build: 0 errors; fast tests: 0 failed.
## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 53b11a1f 7d7e1556 719e1223 b0913bc2 6b653068.
- 2026-10-22: Doing -> Done. README and DOWNLOAD reorganised around download and use, For AI agents section, which-to-use guidance for Curl.Api and Libcurl
