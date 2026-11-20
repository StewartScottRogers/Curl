---
id: BL-5605
title: Confirm whether curl 8.22.0 has a %{filetime} write-out variable, and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/WriteOutTemplateRenderer.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5605 — Confirm whether curl 8.22.0 has a %{filetime} write-out variable, and match it

## Goal

`curl -w '%{filetime}'` behaves as curl 8.22.0 does: either the port supports the variable with curl's output, or it refuses it with curl's exact message.

## Context

From Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #4. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

The cookbook reports `curl -w '%{filetime}'` refused with `unknown --write-out variable: 'filetime'` and says upstream has it. curl's documented write-out variable list (`docs/cmdline-opts/write-out.md` in the 8.22.0 tarball, `Gap/Baselines/target.json`) is the authority: check it and run curl 8.22.0. If curl has no such variable, the port already matches: say so in the Notes and change nothing except a test pinning the refusal text. If curl has it, add it (the transfer already knows the file time for `-R`), keep `--ai-help`'s write-out section right, and pin it.

## Acceptance criteria

- [x] The Notes quote what curl 8.22.0's write-out documentation and binary do with `%{filetime}`.
- [x] The port's output for `-w '%{filetime}'` (with and without `-R`) matches curl 8.22.0's, pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured real curl 8.22.0 (Schannel build): `curl -w "[%{filetime}]" file:///...` with and without `-R` writes `[]`, prints `curl: unknown --write-out variable: filetime` and exits 37; `curl -M` (the manual) lists no filetime variable. The port already matches, so no production change; only a test pins the refusal (Curl.Output.UnitTests WriteOutTemplateRendererAdversarialTests). The real binary is the authority. The cookbook report was wrong that upstream has it. Nothing for --ai-help to change.

## Log

- 2026-11-20: Created from Surly bug reports (2203 #4).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 51919ce1 189e5f61 3d836d5e.
- 2026-11-20: Doing -> Done. curl 8.22.0 has no filetime variable; port matches; refusal pinned by test
