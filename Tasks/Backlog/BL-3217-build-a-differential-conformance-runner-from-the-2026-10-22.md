---
id: BL-3217
title: Build a differential conformance runner from the 2026-10-22 tester's cases, in C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary, Curl.Conformance.Differential.UnitTests, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3217 — Build a differential conformance runner from the 2026-10-22 tester's cases, in C#

## Goal

A C# runner (no Python) replays the tester's ~1,700 CLI and ~41,000 Libcurl differential cases against Curl and a reference curl 8.22.0 on local mock servers and reports each difference, so these findings stay fixed; and it adds cases for WebSocket (`curl_ws_*`), `curl_formadd` and `Curl.Api`, which the tester did not cover.

## Context

Stewart reported on 2026-10-22 (prioritised findings list, item "Other decisions": CI differential harness). The tester's harness and data are preserved outside the repo in `Z:\repos\Curl.difftest\2026-10-22\` (Python scripts as a reference only - never committed or run, CLAUDE.md's no-Python rule; `scen.json`, `h_in.json`, `pf_in.json`, `m_in.json` are the case inputs; `LibTest\` is a C# driver over Libcurl; `report*.txt`, `lib_summary.txt`, `pf_report.txt` are its outputs; `pki\` its test certificates). Port the mock servers to C# (or reuse `Record-CurlExchange.ps1` and the conformance harness's servers), keep the reference-build quirks the tester excluded (ANSI args, 32-bit time_t, the debug build's `sendrecv_dl()` lines, no HTTP/2, broken IDN) as named exclusions, and decide in an ADR marked "Decided by Claude under Stewart's delegation" whether it runs in CI (as a job on Windows that downloads curl 8.22.0) or on demand, and how it relates to the gap office's behaviour measurement. Use `/new-project` for the projects; the solution folder is `/Conformance/Differential/`. A large task: split it with `task-planner` into one task per area (CLI HTTP, CLI protocols, TLS, Libcurl easy, multi, share, MIME, printf, WebSocket, formadd, Curl.Api) if it is more than one run.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] The ADR exists and is in the index, naming where the runner runs and its exclusions.
- [ ] The runner replays at least the CLI HTTP cases from `scen.json` against both binaries and writes a report in the tester's `### DIFF` shape; the remaining areas are either covered or filed as follow-up tasks.
- [ ] New cases for WebSocket, `curl_formadd` and `Curl.Api` exist (or are filed as tasks with their scope).
- [ ] No Python is committed; the projects build clean with -warnaserror and their fast tests pass; Measure-CodeQuality.ps1 0 failing members for any new UnitLibrary.

## Notes

## Log

- 2026-10-22: Created.
