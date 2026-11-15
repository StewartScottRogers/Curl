---
id: BL-5224
title: Fix AF-0775: ADR-1238 (Accepted) still decides that alt-svc allows h1, h2 and h3 off Windows with no version option; since ADR-1268 the code allows h3 only on a QUIC build, which the executable never is
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-14
completed:
---
# BL-5224 — Fix AF-0775: ADR-1238 (Accepted) still decides that alt-svc allows h1, h2 and h3 off Windows with no version option; since ADR-1268 the code allows h3 only on a QUIC build, which the executable never is

## Goal

The defect the audit office reported as AF-0775 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0775 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md:23`

Location: `Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md:23`

ADR-1238 is 'Status: Accepted' and its Decision says: '`AltSvcTransferCache.LookupOf` allows only `h1` as the destination when the transfer has no version option and the system is Windows; elsewhere it still allows `h1`, `h2` and `h3`, as the OpenSSL builds with nghttp2 and ngtcp2 do.' The code says otherwise: Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:291 `isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns;` with line 73 `NoQuicBuildAlpns = new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2 }`. OpenAsync's `actsAsQuicBuild` defaults to false (line 132), and nothing outside AltSvcTransferCache.cs uses it, so a transfer with no version option never switches to h3 off Windows. ADR-1268 made this change and cites ADR-1238 ('Curl offered h3 off Windows (ADR-1238)'), but ADR-1238 is still marked Accepted and is not marked as amended, so an agent reading it would believe h3 alternatives are taken off Windows.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md -Pattern 'elsewhere it still allows'; Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'actsAsQuicBuild \? EveryAlpn : NoQuicBuildAlpns'
```

- Expected: No match in ADR-1238 (or ADR-1238 marked amended by ADR-1268), because the code gates h3 off Windows on actsAsQuicBuild.
- Actual: ADR-1238...md:23: 'version option and the system is Windows; elsewhere it still allows `h1`, `h2` and `h3`, as the' and AltSvcTransferCache.cs:291: 'isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns;'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
