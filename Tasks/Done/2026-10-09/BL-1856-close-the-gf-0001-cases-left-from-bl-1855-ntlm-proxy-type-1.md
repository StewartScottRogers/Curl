---
id: BL-1856
title: Close the GF-0001 cases left from BL-1855: NTLM proxy type-1 through the tunnel, CONNECT reply errors, test2043 revocation
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1857, BL-1858]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1856 — Close the GF-0001 cases left from BL-1855: NTLM proxy type-1 through the tunnel, CONNECT reply errors, test2043 revocation

## Goal

The eight GF-0001 upstream cases BL-1855 left failing over the in-process `TcpConnector` pass in `UpstreamConformanceTests` and are listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (test2043 may instead be skipped with a stated reason).

## Context

- Split from BL-1855, which fixed test1293 (the harness's dialer now refuses the unspecified address) and test213's `CONNECT ... HTTP/1.0` line (`--proxy1.0 http://A` keeps HTTP/1.0 in `Curl.Console/TransferProxySelection.cs`).
- First differences measured 2026-10-08 with `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration&FullyQualifiedName~UpstreamConformanceTests"`:
  - test1008, test1021, test209, test265, test213: expected `Proxy-Authorization: NTLM TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` (curl's own NTLM type-1: flags 0x00088206, no version), got `TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (flags 0xA2088207 plus a version block, the SSPI-style message). The cases' `<features>` say `!SSPI`: either the tunnel must use the hand-built type-1 these cases expect, or the harness should screen `SSPI` as a Windows feature. The type-1 builder lives in `Curl.Ntlm.UnitLibrary` / `Curl.Authentication.UnitLibrary`, which may need adding to `touches`.
  - test217: `--write-out` expected `000 405`, got `000 000` (`%{http_connect}` after a refused CONNECT).
  - test750: expected `curl: (43) Invalid response header`, got `curl: (56) Proxy CONNECT aborted`.
  - test1715: expected exit 56, got 7.
  - test2043: `--ssl-no-revoke -I https://revoked.badssl.com/` expects exit 0, got 6: it needs the network; decide whether the harness skips it.

## Acceptance criteria

- [x] Upstream test1008 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test1021 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test209 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test265 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test213 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test217 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test750 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test1715 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test2043 passes and is listed, or is skipped by the harness with a stated reason.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-10-08 (lane 1): measured the nine cases (all still Inconclusive, unlisted). Split the work into BL-1857 (CONNECT reply errors: test217, test750, test1715, in Curl.Networking) and BL-1858 (NTLM type-1 on Windows: test1008, 1021, 209, 213, 265; and test2043), with the measured causes in each. This run's budget could not cover the fixes; BL-1856 closes once both are Done.
- 2026-10-08 (lane 1, second claim): BL-1857 and BL-1858 are Done. test1008, 1021, 209, 213, 217, 265, 750 and 1715 are listed in `PassingUpstreamCases.txt` and pass; test2043 is screened by `UpstreamCaseScreening` as needing the internet (`UpstreamCaseScreeningTests.cs`). Verified: `dotnet build -warnaserror` clean, fast tests green. No code change needed here.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Backlog. Split into BL-1857 (CONNECT reply errors) and BL-1858 (NTLM type-1 on Windows, test2043); waits on both
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Eight GF-0001 cases pass and are listed (via BL-1857, BL-1858); test2043 screened as needing the internet
