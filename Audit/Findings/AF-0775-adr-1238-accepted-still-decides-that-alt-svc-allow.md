---
id: AF-0775
title: ADR-1238 (Accepted) still decides that alt-svc allows h1, h2 and h3 off Windows with no version option; since ADR-1268 the code allows h3 only on a QUIC build, which the executable never is
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md:ADR-1238:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0775 - ADR-1238 (Accepted) still decides that alt-svc allows h1, h2 and h3 off Windows with no version option; since ADR-1268 the code allows h3 only on a QUIC build, which the executable never is

## Summary

Medium finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md:23`: ADR-1238 (Accepted) still decides that alt-svc allows h1, h2 and h3 off Windows with no version option; since ADR-1268 the code allows h3 only on a QUIC build, which the executable never is. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md:23`

ADR-1238 is 'Status: Accepted' and its Decision says: '`AltSvcTransferCache.LookupOf` allows only `h1` as the destination when the transfer has no version option and the system is Windows; elsewhere it still allows `h1`, `h2` and `h3`, as the OpenSSL builds with nghttp2 and ngtcp2 do.' The code says otherwise: Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:291 `isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns;` with line 73 `NoQuicBuildAlpns = new HashSet<AltSvcAlpn> { AltSvcAlpn.H1, AltSvcAlpn.H2 }`. OpenAsync's `actsAsQuicBuild` defaults to false (line 132), and nothing outside AltSvcTransferCache.cs uses it, so a transfer with no version option never switches to h3 off Windows. ADR-1268 made this change and cites ADR-1238 ('Curl offered h3 off Windows (ADR-1238)'), but ADR-1238 is still marked Accepted and is not marked as amended, so an agent reading it would believe h3 alternatives are taken off Windows.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1238-alt-svc-switches-to-h1-alone-without-a-version-option-on-windows.md -Pattern 'elsewhere it still allows'; Select-String -Path Curl.Engine.UnitLibrary/AltSvcTransferCache.cs -Pattern 'actsAsQuicBuild \? EveryAlpn : NoQuicBuildAlpns'
```

- Expected: No match in ADR-1238 (or ADR-1238 marked amended by ADR-1268), because the code gates h3 off Windows on actsAsQuicBuild.
- Actual: ADR-1238...md:23: 'version option and the system is Windows; elsewhere it still allows `h1`, `h2` and `h3`, as the' and AltSvcTransferCache.cs:291: 'isWindows ? SchannelBuildAlpns : actsAsQuicBuild ? EveryAlpn : NoQuicBuildAlpns;'

## Re-audits

## Log

- 2026-11-14: filed proposed.
