---
id: BL-3242
title: Add differential cases for Curl.Api against the equivalent curl command lines
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3233]
touches: [Curl.Conformance.Differential.UnitLibrary/Api, Curl.Conformance.Differential.UnitTests/Api, Curl.Conformance.Differential.IntegrationTests/Api, Curl.Conformance.Differential.IntegrationTests/Curl.Conformance.Differential.IntegrationTests.csproj, Curl.Conformance.Differential.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-22
completed: 2026-10-23
---
# BL-3242 — Add differential cases for Curl.Api against the equivalent curl command lines

## Goal

New differential cases run `Curl.Api` requests beside the equivalent curl 8.22.0 command lines on BL-3232's loopback routes and report where the wire bytes or results differ.

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3231. The tester did not cover Curl.Api. Reuse BL-3233's CLI HTTP cases where a Curl.Api call expresses the same transfer.

## Acceptance criteria

- [x] Curl.Api cases for the common request shapes (GET, POST forms, headers, redirects, auth, cookies) replay beside curl; one run's report is recorded under Notes, each difference filed.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.
- 2026-10-23 lane-5 run. Design (ADR-0790): the cases are data in `Curl.Conformance.Differential.UnitLibrary/Api` (`ApiDifferentialCases.All`, 20 cases: GET, custom and empty headers, User-Agent, `-d` form posts, a Content-Type override, `-F` multipart, PUT, DELETE, redirects unfollowed and followed incl. 303 and 307 POSTs, Basic right and wrong, Digest, a `Cookie` header, the cookie engine across `/redir-cookie`), each mirroring a CLI HTTP case. `Curl.Conformance.Differential.IntegrationTests/Api` sends each through `Curl.Api` in process and its command line (with `-A '' -w '\n%{response_code}'` first: only the curl tool sends a User-Agent) through the reference, comparing wire, stdout (body plus response code), stderr and exit code (the `CurlException` code). The library does not reference `Curl.Api`, so it keeps its gates.
- Touches widened: the IntegrationTests csproj (it now references `Curl.Api`) and the library's `CLAUDE.md` (its Shape list gains `Api/`). No task in Doing on origin/work/dark-factory names either (checked BL-3235, 3236, 3237, 3284).
- The run: `CURL_DIFFERENTIAL_REFERENCE='C:\Program Files\Git\ucrt64\bin\curl.exe' dotnet test Curl.Conformance.Differential.IntegrationTests --filter FullyQualifiedName~ApiDifferentialTests`, 1 s, passed. First run: one `### DIFF`, `api-headers-remove` (`-H 'Accept:'`): wire ours `GET /echo HTTP/1.1\r\nHost: 127.0.0.1:18080\r\nAccept:\r\n\r\n`, real without the `Accept` line. That was the case's mapping, not a fault: `Curl.Api` documents an empty value as `Name;` (sent empty), so the case became `api-headers-empty` with `-H 'Accept;'`; `Curl.Api` has no way to remove a default header, filed as BL-3294. Second run: the report is empty, 0 `### DIFF`, 0 `### SLOW` across the 20 cases.
- Curl.Conformance.Differential.UnitTests: 234 passed. Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members. The new tests touch no file system, path, OS error or certificate and the Integration test is inconclusive without the reference, so the change is platform-neutral for Linux and macOS CI.

## Log

- 2026-10-22: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 38f86ed0 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. Curl.Api's common request shapes replay beside the equivalent curl 8.22.0 command lines in the differential runner; 0 differences, header removal gap filed as BL-3294
