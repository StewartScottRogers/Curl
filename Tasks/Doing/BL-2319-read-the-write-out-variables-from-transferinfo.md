---
id: BL-2319
title: Read the --write-out variables from TransferInfo
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2317, BL-2318]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2319 — Read the --write-out variables from TransferInfo

## Goal

`-w` (`--write-out`) takes every libcurl-measured variable from the transfer's `TransferInfo`, as upstream's tool takes them from `curl_easy_getinfo`; the tool's own variables (`%{urlnum}`, `%{filename_effective}`, `%{exitcode}`, `%{errormsg}`, `%{onerror}`, `%{stderr}`/`%{stdout}`, headers through the header API) stay where they are; output is byte for byte unchanged.

## Context

ADR-0506, plan phase 2 step 5; needs BL-2317 and BL-2318. `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` builds the variables today from the transfer report and runner state (`CurlCommandRunner.WriteOutVariables`). `Curl.Output.UnitLibrary` gains a reference to `Curl.Api.UnitLibrary` (allowed: the seam references no protocol library). Upstream's split: `src/tool_writeout.c` reads `CURLINFO_*` for each libcurl variable.

## Acceptance criteria

- [ ] `TransferWriteOutVariables` (or its successor) is built from `TransferInfo` for every variable upstream reads with `curl_easy_getinfo`; a test per variable group (times, sizes, speeds, addresses, HTTP, TLS, redirects, IDs) builds it from a `TransferInfo` and checks the formatted text.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the existing `-w` tests in `Curl.Console.UnitTests` and `Curl.Output.UnitTests` pass unchanged).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
