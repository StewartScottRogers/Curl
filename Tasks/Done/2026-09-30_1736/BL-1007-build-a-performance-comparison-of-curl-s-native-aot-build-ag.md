---
id: BL-1007
title: Build a performance comparison of Curl's native AOT build against real curl
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000, BL-1003]
touches: [Audit/Tools/Measure-Performance.ps1]
lane: no
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-1007 — Build a performance comparison of Curl's native AOT build against real curl

## Goal

`Audit/Tools/Measure-Performance.ps1` publishes Curl.Console as a native AOT binary, runs a fixed set of transfers through it and through real curl the same number of times, and writes a table of median and 90th-percentile wall time and peak memory for each, plus startup time and binary size.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1007`.
The performance auditor (BL-1011) uses it. Timing and memory come from
`Record-CurlExchange.ps1`'s `timing.json` (BL-1003), so both binaries are measured by the
same code against the same canned loopback server.

Publishing: `dotnet publish Curl.Console -c Release -o <repo>.audit\publish-<stamp>`
produces the native binary by default; on Windows it needs `C:\Program Files
(x86)\Microsoft Visual Studio\Installer` on `PATH` for vswhere (CLAUDE.md), so the script
adds it when that folder exists. A publish failure is itself a result: report it and
stop with exit 1.

Fixed scenarios (the list lives in the script; changing it is a new scorecard series):

1. `startup`: `--version`, no server (`-NoServer`).
2. `small-get`: `-s -o <out> http://127.0.0.1:<port>/` with a 1 KiB `Content-Length` body.
3. `large-get`: the same with a 50 MiB body. If `Record-CurlExchange.ps1` cannot serve a
   response that large from its `-Response` string, use the largest it serves and record
   the size in the output.
4. `headers-verbose`: `-sv` with 50 response headers.
5. `chunked`: a chunked response of 1000 chunks.
6. `redirects`: `-sL` through 5 redirects (`-Connections 6`).

Each scenario runs `-Iterations` times (default 20) per binary, alternating the two so
machine noise hits both equally, after 2 discarded warm-up runs each. Output:
`performance.json` with, per scenario and binary, `medianMs`, `p90Ms`,
`medianPeakWorkingSetBytes`, and the binaries' sizes and versions; plus a Markdown table
in the fixed row order above, which the scorecard (BL-1017) copies.

## Acceptance criteria

- [x] A run with `-Iterations 3` publishes Curl.Console, runs all six scenarios for both binaries, and writes `performance.json` and the Markdown table with every cell filled.
- [x] `performance.json` records the reference curl's `--version` first line, Curl's commit, both binaries' sizes in bytes, and the machine's processor count.
- [x] Runs alternate binaries (visible in the per-run log the script writes beside the JSON).
- [x] A forced publish failure (e.g. `-PublishArguments '-p:DoesNotExist=<bad>'` or an invalid `-Runtime`) ends with exit 1 and a message, not a partial table.
- [x] Header help documents parameters, scenarios and output; ASCII only.

## Notes

- On the audit branch (worktree Z:/repos/Curl.auditbranch), commit 97f289a1, pull request https://github.com/StewartScottRogers/Curl/pull/35.
- -Iterations 3 run (32 processors, reference curl 8.21.0 Schannel, Curl at 179445bd published native AOT): all six scenarios for both binaries, every cell of performance.md filled; performance.json holds both --version first lines, the commit, both binary sizes (curl 328876, Curl 16021504) and the processor count; runs.log shows the binaries alternating with the first swapped each iteration. Medians curl/Curl ms: startup 36/37, small-get 64/78, large-get 146/224, headers-verbose 55/55, chunked 48/56, redirects 52/69.
- large-get serves the full 50 MiB: Record-CurlExchange.ps1 decodes its -Response text in PowerShell at about 1 MiB/s (measured 4 MiB in 3 s) before curl starts, so each large-get run spends about 40 s preparing; it is called in-process so the body never passes through a command line.
- Forced publish failure (-Runtime not-a-runtime): NETSDK1083, exit 1, "No measurements were taken", empty output folder.
- After the measured run: warm-up labels fixed (they were numbered in reverse), run folders named by label, and the recorder's per-run line silenced. Checked by parse and by the label logic, not by a second full run.
- Interpret with care (in the header help): curl's --version run reports 0 bytes peak, being shorter than the recorder's 10 ms memory sample; curl.exe keeps libcurl in a separate DLL, so binary sizes compare an executable with a whole program.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Measure-Performance.ps1 times six fixed transfers through Curl's native build and real curl; in PR #35, awaiting Stewart's merge.
