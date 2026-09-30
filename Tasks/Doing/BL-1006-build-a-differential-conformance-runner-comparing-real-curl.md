---
id: BL-1006
title: Build a differential conformance runner comparing real curl and Curl on generated command lines
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1000]
touches: [Audit/Tools/Invoke-DifferentialConformance.ps1]
lane: no
requirement: none
created: 2026-09-29
completed:
---
# BL-1006 — Build a differential conformance runner comparing real curl and Curl on generated command lines

## Goal

`Audit/Tools/Invoke-DifferentialConformance.ps1` generates reproducible command lines, runs each through real curl and through Curl.Console against the same canned loopback exchange, and reports every case where the request bytes, stdout, stderr or exit code differ.

## Context

Interactive only (`lane: no`): it writes `Audit/`. Run it with `/task-run BL-1006`.
The conformance auditor (BL-1012) uses it. It builds on what exists rather than
replacing it: `Record-CurlExchange.ps1` already runs the loopback server, records
`request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt`, and runs any executable
through `-Curl` (default: the reference curl 8.21.0 from Git for Windows' mingw64, per
ADR-0009 and ADR-0018). The `conformance-auditor` agent's rule stands: state the curl
version every comparison was made against (`curl --version`'s first line).

Design:

- Parameters: `-Count` (default 100), `-Seed` (default 0), `-Curl` (reference; default as
  `Record-CurlExchange.ps1`), `-Candidate` (default: `Curl.Console` built with `dotnet
  build Curl.Console -c Release`, its output executable), `-Options` (restrict to named
  options), `-OutDirectory`.
- Generator: from an option grammar built by parsing `curl --help all` of the reference
  (option names, short aliases, and whether each takes an argument), choose 1 to 4
  options per case with seeded `System.Random`, values drawn from a small typed pool per
  argument kind (numbers including 0, -1 and huge; headers; empty string; paths under
  the output directory; `@file` forms), plus one `http://127.0.0.1:<port>/` URL. Exclude
  options that reach the network beyond loopback, prompt, or write outside the output
  directory (`--output` values are rewritten into it); the exclusion list is in the
  script with a reason per entry.
- Each case runs `Record-CurlExchange.ps1` twice, same `-Port`, same `-Response`, once
  per executable, into `case-<n>/curl` and `case-<n>/candidate`.
- Comparison: byte-for-byte on the four files. Normalise only what legitimately differs
  between two runs of the same binary - the program name in stderr (`curl:` vs the
  candidate's name, if different), dates in `Date:` headers, and the loopback port - and
  list those normalisations in the report.
- Output: `summary.json` (`{ referenceVersion, candidateCommit, seed, count, same,
  different, cases: [ { n, args, differences: [ "exitcode" | "stdout" | "stderr" |
  "request" ] } ] }`) and, per differing case, a `repro.ps1` with the two exact
  `Record-CurlExchange.ps1` commands.

## Acceptance criteria

- [ ] `-Count 10 -Seed 1` produces `summary.json` with 10 cases, the reference curl's version string, and a `repro.ps1` for each differing case.
- [ ] Two runs with the same seed generate the same 10 argument lists.
- [ ] `-Candidate` set to the reference curl itself reports 0 differences for `-Count 20` (the normalisations are sufficient and no more).
- [ ] No generated case contacts a host other than 127.0.0.1 (the exclusion list covers proxies, `--resolve`, `--connect-to` and similar, each with a reason).
- [ ] Header help documents parameters, the generator, the normalisations and the output; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
