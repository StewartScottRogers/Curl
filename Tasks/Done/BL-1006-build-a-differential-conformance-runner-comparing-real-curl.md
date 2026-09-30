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
completed: 2026-09-30
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

- [x] `-Count 10 -Seed 1` produces `summary.json` with 10 cases, the reference curl's version string, and a `repro.ps1` for each differing case.
- [x] Two runs with the same seed generate the same 10 argument lists.
- [x] `-Candidate` set to the reference curl itself reports 0 differences for `-Count 20` (the normalisations are sufficient and no more).
- [x] No generated case contacts a host other than 127.0.0.1 (the exclusion list covers proxies, `--resolve`, `--connect-to` and similar, each with a reason).
- [x] Header help documents parameters, the generator, the normalisations and the output; ASCII only; runs under PowerShell 7 and Windows PowerShell 5.1.

## Notes

- On the audit branch (worktree Z:/repos/Curl.auditbranch), commit 4fc86399, pull request https://github.com/StewartScottRogers/Curl/pull/34.
- -Count 10 -Seed 1 -ListOnly: the same 10 argument lists on two runs and under Windows PowerShell 5.1 and PowerShell 7.6.6 (cases are generated with a placeholder port, filled in after). 500 generated cases (seed 7) name only http://127.0.0.1.
- Reference against itself, -Count 20 -Seed 1: first 18 same / 2 different, which exposed two things that vary between runs of the same binary beyond the spec's list: --haproxy-protocol's PROXY line carries curl's ephemeral source port, and progress-meter rows made only of numbers. Both normalised and documented; rerun: 20 same, 0 different.
- Reference curl 8.21.0 (Schannel) against Curl.Console Release at f2e90587, -Count 10 -Seed 1: 8 same, 2 different, each with case-<n>/repro.ps1. Findings for BL-1012 to raise: (case 2) Curl refuses --libcurl as unsupported by the installed libcurl, where curl accepts it and refuses the blank --proto-default instead; (case 10) with --http2-prior-knowledge Curl sends the HTTP/2 preface and exits 16, where the reference build has no HTTP/2 and exits 2.
- Normalisations beyond the spec's three (program name, Date, port): multipart boundary, --haproxy-protocol source port, progress-meter lines. Each changes only what two runs of the same binary produce differently; the reference-against-itself run is the check.
- ASCII only. The candidate defaults to Curl.Console built -c Release in this repository (curl.exe).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Invoke-DifferentialConformance.ps1 compares real curl and Curl on seeded loopback cases, 0 differences reference-against-itself; in PR #34, awaiting Stewart's merge.
