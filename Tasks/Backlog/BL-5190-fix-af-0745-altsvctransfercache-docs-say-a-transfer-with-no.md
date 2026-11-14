---
id: BL-5190
title: Fix AF-0745: AltSvcTransferCache docs say a transfer with no version option may switch to h3 off Windows; since ADR-1268 it may not unless actsAsQuicBuild is set
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5190 — Fix AF-0745: AltSvcTransferCache docs say a transfer with no version option may switch to h3 off Windows; since ADR-1268 it may not unless actsAsQuicBuild is set

## Goal

The defect the audit office reported as AF-0745 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0745 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:285`

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:285`

The summary of NoVersionOptionDestinations says 'h1 alone on Windows, as the Schannel build, and h1, h2 and h3 elsewhere, as the OpenSSL builds'. Its body is 'isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns', and line 73 sets NoQuicBuildAlpns to { H1, H2 }. actsAsQuicBuild defaults to false in OpenAsync (line 132), and ADR-1268 says the executable never sets it. The type's remarks table (line 26) still gives 'none: h2, h1; h1, h2, h3', and line 39 says an h3 entry makes such a transfer race HTTP/3 against TCP. Both describe the pre-BL-5099 behaviour.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'and <c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere', 'actsAsQuicBuild \? EveryAlpn : NoQuicBuildAlpns', 'NoQuicBuildAlpns = new'
```

- Expected: The summary says h1 and h2 off Windows, plus h3 only when the build acts as a QUIC build.
- Actual: The summary says '<c>h1</c>, <c>h2</c> and <c>h3</c> elsewhere' while the body returns NoQuicBuildAlpns = { H1, H2 } unless actsAsQuicBuild.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
