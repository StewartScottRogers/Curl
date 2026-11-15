---
id: AF-0745
title: AltSvcTransferCache docs say a transfer with no version option may switch to h3 off Windows; since ADR-1268 it may not unless actsAsQuicBuild is set
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1801.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-14_1335.md, 2026-11-14_1801.md).
key: truthfulness:Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:NoVersionOptionDestinations:false-doc-comment
reproduction: none
task: BL-5190
tasks: BL-5190
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: consecutive
closed-by: 2026-11-14_1335.md, 2026-11-14_1801.md
---
# AF-0745 - AltSvcTransferCache docs say a transfer with no version option may switch to h3 off Windows; since ADR-1268 it may not unless actsAsQuicBuild is set

## Summary

Medium finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:285`: AltSvcTransferCache docs say a transfer with no version option may switch to h3 off Windows; since ADR-1268 it may not unless actsAsQuicBuild is set.

## Evidence

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:285`

The summary of NoVersionOptionDestinations says 'h1 alone on Windows, as the Schannel build, and h1, h2 and h3 elsewhere, as the OpenSSL builds'. Its body is 'isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns', and line 73 sets NoQuicBuildAlpns to { H1, H2 }. actsAsQuicBuild defaults to false in OpenAsync (line 132), and ADR-1268 says the executable never sets it. The type's remarks table (line 26) still gives 'none: h2, h1; h1, h2, h3', and line 39 says an h3 entry makes such a transfer race HTTP/3 against TCP. Both describe the pre-BL-5099 behaviour.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'and <c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere', 'actsAsQuicBuild \? EveryAlpn : NoQuicBuildAlpns', 'NoQuicBuildAlpns = new'
```

- Expected: The summary says h1 and h2 off Windows, plus h3 only when the build acts as a QUIC build.
- Actual: The summary says '<c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere' while the body returns NoQuicBuildAlpns = { H1, H2 } unless actsAsQuicBuild.

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the Select-String. 'and <c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere' has no match. The code patterns match AltSvcTransferCache.cs:73 (NoQuicBuildAlpns = { H1, H2 }) and :291 (isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns). The type's remarks (line 26) now say no version option switches to h1, h2 'and h3 only when the build acts as a QUIC build, ADR-1268'. NoVersionOptionDestinations' doc (lines 287-288) says the same.
- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Select-String found no match for 'and <c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere'. It matched line 73 'NoQuicBuildAlpns = new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2 };' and line 291 'isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns;'. The remarks table at line 26 now reads 'none: h2, h1; h1, h2 (and h3 only when the build acts as a QUIC build, ADR-1268)', and lines 287-288 say the same.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-14_1335.md, 2026-11-14_1801.md).
