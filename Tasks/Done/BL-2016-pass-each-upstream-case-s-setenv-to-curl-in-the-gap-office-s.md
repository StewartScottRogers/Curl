---
id: BL-2016
title: Pass each upstream case's setenv to Curl in the gap office's upstream-case measuring tool through InProcessCurl's environment overload
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: []
lane: no
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2016 — Pass each upstream case's setenv to Curl in the gap office's upstream-case measuring tool through InProcessCurl's environment overload

## Goal

The gap office's upstream-case measuring tool (the file-based app GF-0047's reproduce command runs) runs each case with the case's `<client><setenv>` as Curl's whole environment, so the next gap analysis measures GF-0047's items (`behaviour:test1249`, 436, 724, 725, 731, 740, 741) as `match`.

## Context

- Split from BL-1977 (gap finding GF-0047). A dark factory lane may not read or change the gap office's folder (ADR-0433), so this is interactive only.
- BL-1977 added `InProcessCurl.RunAsync(arguments, standardOutput, standardError, standardInput, tcpDialer, dnsResolver, datagramConnector, readEnvironmentVariable)`: the dialing overload with the environment the run reads - the proxy variables and `NO_PROXY`, `CURL_HOME`, `XDG_CONFIG_HOME` and `HOME` for the default config file, and the IPFS gateway variables and files. The tool calls an overload without it today and ignores `UpstreamCurlInvocation.EnvironmentVariables`.
- Call the new overload with `name => invocation.EnvironmentVariables.GetValueOrDefault(name)`, as `Curl.Conformance.UnitTests`' `UpstreamConformanceTests.RunCurlAsync` passes it. Those seven cases pass there now (BL-1977); test433 waits on BL-2017.
- Reproduce with the command in GF-0047's evidence, for cases 1249,433,436,724,725,731,740,741.

## Acceptance criteria

- [x] The tool passes each case's environment to `InProcessCurl`, and GF-0047's reproduce command reports test1249, 436, 724, 725, 731, 740 and 741 as passing.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. The gap tool runs each upstream case with its setenv as Curl's environment; test1249, 433, 436, 724, 725, 731, 740 and 741 now pass (gap PR #108, a7329e849)
