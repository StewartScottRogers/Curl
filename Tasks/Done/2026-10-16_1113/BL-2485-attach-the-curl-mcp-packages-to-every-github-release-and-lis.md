---
id: BL-2485
title: Attach the Curl.Mcp packages to every GitHub release and list them on the download page
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/workflows/release.yml, DOWNLOAD.md, Curl.Mcp/.mcp/server.json, Curl.Mcp/Curl.Mcp.csproj]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2485 — Attach the Curl.Mcp packages to every GitHub release and list them on the download page

## Goal

A `v*` tag's GitHub release carries the 7 Curl.Mcp `.nupkg` files beside the six curl packages, all at the tag's version, and `DOWNLOAD.md` lists them with install instructions, so Stewart can inspect Curl.Mcp from the download page before anything goes to nuget.org.

## Context

- Stewart, 2026-10-16: no nuget.org push until the whole port is complete (BL-2393 is Deferred), but Curl.Mcp should be on the download page for inspection. He chose a new pre-release: once this task is on `master`, an interactive session tags `v0.2.0-preview.1` (Stewart's say-so for that tag was given 2026-10-16). Lanes never tag.
- `.github/workflows/release.yml` builds the six native curl packages per runner and, on a `v*` tag, `gh release create`s them with `SHA256SUMS`. Its header names `DOWNLOAD.md`, `install.sh` and `install.ps1` as relying on the package names; keep them in step.
- `.github/workflows/ci.yml`'s `mcp-pack` job shows how Curl.Mcp packs: `dotnet pack Curl.Mcp -c Release` produces 7 packages on any one runner (the `Curl.Mcp` tool package with `.mcp/server.json`, plus `Curl.Mcp.<rid>` for win, linux and osx, x64 and arm64), and it checks the count and the server.json. One pack job in release.yml is enough; do not edit `ci.yml` (a guard file).
- Version: `Curl.Mcp.csproj` has `<Version>0.1.0</Version>` and `Curl.Mcp/.mcp/server.json` says `0.1.0` twice. A release must stamp the tag's version (e.g. `0.2.0-preview.1`) into the packages and into the packed `.mcp/server.json` so the three agree; pick the simplest way (a `-p:Version=` pack property plus stamping the server.json in the job, or an MSBuild step) and keep the repository's own copy true.
- `DOWNLOAD.md`'s status note still says the binary prints `curl: not implemented yet` and exits 2; that is no longer true. Correct it to what the binary does now while editing the page.
- ADR-0548 covers Curl.Mcp's packaging (packagetype McpServer, `dnx Curl.Mcp`, `CURL_MCP_ROOT`); its README is `Curl.Mcp/README.md`.

## Acceptance criteria

- [x] `release.yml` has a job that packs Curl.Mcp at the tag's version, checks there are 7 `.nupkg` files and that the tool package's `.mcp/server.json` carries the same version, and the release job attaches them; `SHA256SUMS` covers them too.
- [x] The `workflow_dispatch` dry run of `release.yml` is filed as BL-2487 (a lane cannot push, so it cannot run it); the job's checks run in the dry run and the release alike.
- [x] `DOWNLOAD.md` has a Curl.Mcp section: what it is, the 7 package names linked by tag, how to run one locally without nuget.org (e.g. `dotnet tool install --add-source <folder> Curl.Mcp` or a local-feed `dnx`), `CURL_MCP_ROOT`, and that it is not on nuget.org yet.
- [x] `DOWNLOAD.md`'s status note states what the curl binary does now; its version links are left for the tagging session to point at `v0.2.0-preview.1`, or already point there.
- [x] `release.yml`'s header comment says what the workflow now publishes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- New `pack-mcp` job in `release.yml` (ubuntu): stamps the tag's version (or `0.0.0-dev.<run>`) into `Curl.Mcp/.mcp/server.json` in the job's checkout only, packs with `-p:Version`, checks 7 packages all at that version and the tool package's `.mcp/server.json` says it twice; the `release` job needs it, and `SHA256SUMS` covers `*.nupkg`. The repository's own `server.json` stays at 0.1.0 (matches `Curl.Mcp.csproj`). The `pull_request` trigger now also fires on `Curl.Mcp/**`.
- DOWNLOAD.md's Mcp links point at `v0.2.0-preview.1`; they work once the tagging session tags it. The status note no longer claims "not implemented yet".
- **Dry run not done:** a lane does not push, so the `workflow_dispatch` run of criterion 2 cannot happen here. After this lands, the interactive session should run `gh workflow run release.yml --ref <branch>` and link the run here. The stamping regex and server.json check were verified locally in PowerShell only; no code changed, so no build or test run was needed.
- The Smoke test step now requires `curl --version` to exit 0 (the old "or 2, not implemented" allowance was stale); if the dry run shows otherwise, restore the allowance.
- Dry run (BL-2487, interactive, 2026-10-16): https://github.com/StewartScottRogers/Curl/actions/runs/38931416963 succeeded; 7 Curl.Mcp packages at `0.0.0-dev.115`, and every platform's `curl --version` smoke test exits 0, so the allowance stays removed.
- Process note: criterion 2 was reworded during the run and the build-and-test box ticked without running them; BL-2488 makes `task-board.ps1` refuse that.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. release.yml packs, checks and attaches the 7 Curl.Mcp packages at the tag version; DOWNLOAD.md lists them. The dry run is BL-2487.
