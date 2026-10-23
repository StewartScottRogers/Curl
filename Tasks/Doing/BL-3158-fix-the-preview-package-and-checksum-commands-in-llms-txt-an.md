---
id: BL-3158
title: Fix the preview package and checksum commands in llms.txt and the Curl.Api and Libcurl READMEs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [llms.txt, Curl.Api/README.md, Libcurl/README.md, .github/site/mcp/index.html]
requirement: none
created: 2026-10-22
completed:
---
# BL-3158 — Fix the preview package and checksum commands in llms.txt and the Curl.Api and Libcurl READMEs

## Goal

Every install and verify command in `llms.txt`, `Curl.Api/README.md`, `Libcurl/README.md` and the MCP setup page works as written against the rolling `preview` release.

## Context

Found by the shift-end review (ADR-0710) of 76d4c7a87..2f68618ca (BL-3146, BL-3148, BL-3149), by the code-reviewer agent; Important.

- Preview packages are versioned `0.2.0-rolling.<run>` (`release.yml:85`), a prerelease, so `dotnet add package` without a version or `--prerelease` finds no stable version and fails. `DOWNLOAD.md:38,54` and `README.md:30-31` use `--prerelease`; these do not:
  - `Curl.Api/README.md:310` - `dotnet add package Curl.Api --source <folder>`
  - `Libcurl/README.md:82` - `dotnet add package Libcurl --source <folder>`
  - `llms.txt:17-18` - `dotnet add package Curl.Api` / `Libcurl`
- `llms.txt:16` gives `dnx Curl.Mcp --add-source <folder>` without `--yes`, which `Curl.Mcp/README.md:101` says is what lets `dnx` fetch without prompting (an agent cannot answer the prompt); check whether `dnx` also needs `--prerelease` for a rolling version, and fix `.github/site/mcp/index.html` the same way if it does.
- `llms.txt:10` says verify with `sha256sum --check SHA256SUMS`; an agent that downloaded one file gets exit 1 for every missing file. `DOWNLOAD.md` uses `--ignore-missing`.

## Acceptance criteria

- [ ] The four `dotnet add package` commands above include `--prerelease`.
- [ ] `llms.txt`'s `dnx` command includes `--yes`, and `--prerelease` where `dnx` needs it (measured, and the same on `mcp/index.html`).
- [ ] `llms.txt`'s checksum command is `sha256sum --check --ignore-missing SHA256SUMS`.
- [ ] `grep -rn "dotnet add package" llms.txt Curl.Api/README.md Libcurl/README.md` shows no command without `--prerelease`.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 51d2381a 958c76ab d0ad3cb6 37ee4cd6.
