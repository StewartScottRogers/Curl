---
id: BL-3217
title: Build a differential conformance runner from the 2026-10-22 tester's cases, in C#
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3218, BL-3219, BL-3220, BL-3221, BL-3222, BL-3223, BL-3224, BL-3225, BL-3226, BL-3227, BL-3228, BL-3229]
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

- 2026-10-22, lane 3: split for size before any code (the task's own instruction and lane rule 4). The tester's inputs are about 680 CLI cases in `cases*.py` (cases.py 380, cases_more.py 228, cases_proto*.py 128, cases_tls.py 70, plus cases_proxy.py) on five Python servers, and the Libcurl inputs `scen.json` (3,289 URL, printf, escape and version scenarios) plus `e_in`, `h_in`, `h_round`, `t1`, `m_in`, `m2`, `m2i`, `m3`, `mi`, `sh`, `mimefile` and `pf_in` JSON files driven by the C# `LibTest` folder. Every part needs a C# port, and the real-binary and socket parts need a host outside the 100%-covered UnitLibrary, which BL-3218's ADR decides. Filed: BL-3218 (ADR, projects, runner core), BL-3219 (HTTP mock routes), BL-3220 (CLI HTTP replay and host), BL-3221 (CLI protocols and proxy), BL-3222 (CLI TLS), BL-3223 (Libcurl scen.json), BL-3224 (Libcurl easy), BL-3225 (multi and share), BL-3226 (MIME and printf), BL-3227 (WebSocket cases), BL-3228 (curl_formadd cases), BL-3229 (Curl.Api cases). Each subtask touches its own subfolder of the new projects so they can run in parallel once BL-3220 lands.
- The second criterion says "CLI HTTP cases from scen.json", but scen.json holds the Libcurl scenarios; the CLI HTTP cases are in cases.py and cases_more.py. BL-3220 ports those, and BL-3223 replays scen.json, so the criterion is met as written once both are Done.
- Once every subtask is Done, this task only verifies: tick the criteria against the subtasks' work and run the build, fast tests and quality gate.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 2ff5cb0a 5b38a42d 04d49e93 c1b5bc31.
- 2026-10-22: Doing -> Backlog. Split for size: waits on BL-3218 to BL-3229 (ADR and runner core, HTTP routes, CLI HTTP/protocol/TLS replay, Libcurl scen/easy/multi/MIME replay, WebSocket, formadd and Curl.Api cases)
