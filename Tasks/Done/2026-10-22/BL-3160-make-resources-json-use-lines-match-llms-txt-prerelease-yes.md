---
id: BL-3160
title: Make resources.json use lines match llms.txt (--prerelease, --yes, --ignore-missing)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/release/New-ResourcesJson.ps1]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3160 — Make resources.json use lines match llms.txt (--prerelease, --yes, --ignore-missing)

## Goal

The `use` line of every entry in the preview release's `resources.json` gives a command that works, and the same command `llms.txt` gives.

## Context

Found by the shift-end review of 2026-10-22 (pr-review-toolkit code-reviewer, review-20261022-180036), diff `8b93cc572...fb9cfd488`.

BL-3158 (commit 396caeaa2) fixed the install and checksum commands in `llms.txt`, `.github/site/mcp/index.html`, `Curl.Api/README.md` and `Libcurl/README.md`. The same shift began tracking `.github/release/New-ResourcesJson.ps1` (commit 6b7834163). Its `use` strings still give the old commands, and `llms.txt` tells agents to choose files from `resources.json`. So the two now give different instructions, which breaks "say what it does, do what it says":

- `.github/release/New-ResourcesJson.ps1:63` (Curl.Mcp): `dnx Curl.Mcp --add-source <folder>`. `llms.txt` says `dnx Curl.Mcp --yes --prerelease --add-source <folder>`.
  - Every preview version is a pre-release (`0.2.0-rolling.N`, `0.0.0-dev.N`), so without `--prerelease` the package does not resolve.
  - Without `--yes`, `dnx` prompts, and an MCP client cannot answer the prompt.
- Lines 68 and 73 (Curl.Api, Libcurl): `dotnet add package Curl.Api` and `dotnet add package Libcurl`, with no `--prerelease`. These fail for the same reason.
- Line 78 (SHA256SUMS): `sha256sum --check SHA256SUMS`. `release.yml` writes SHA256SUMS over every `curl-*` archive and `.nupkg` file, so someone who downloads only one file gets failures for all the rest. `llms.txt` and `DOWNLOAD.md` use `--ignore-missing`.

## Acceptance criteria

- [x] The Curl.Mcp `use` string in `New-ResourcesJson.ps1` contains `dnx Curl.Mcp --yes --prerelease --add-source <folder>`.
- [x] The Curl.Api and Libcurl `use` strings contain `dotnet add package Curl.Api --prerelease` and `dotnet add package Libcurl --prerelease`.
- [x] The SHA256SUMS `use` string contains `sha256sum --check --ignore-missing SHA256SUMS`.
- [x] Running `New-ResourcesJson.ps1` and then `Test-ResourcesJson.ps1` on a folder of dummy release files writes a `resources.json` whose `use` lines match `llms.txt`'s commands word for word, and the test script passes.

## Notes

Only `use` strings changed; verified by running New-ResourcesJson.ps1 then Test-ResourcesJson.ps1 on dummy files (passed); the four `use` lines match llms.txt. No C# changed, so no build or quality run.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 68df6651 e53a5abf 9eac453c 3804208d.
- 2026-10-22: Doing -> Done. use lines match llms.txt; generator and test script pass
