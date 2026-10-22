---
id: BL-3073
title: Fix AF-0320: ITlsProvider's remarks say the production implementation delegates to SslStream rather than implementing TLS; a second, hand-built production provider exists
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3073 — Fix AF-0320: ITlsProvider's remarks say the production implementation delegates to SslStream rather than implementing TLS; a second, hand-built production provider exists

## Goal

The defect the audit office reported as AF-0320 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0320 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0320-itlsprovider-s-remarks-say-the-production-implemen.md`.

Location: `Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs:11`

Location: `Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs:11`

Remarks, lines 11-12: 'The production implementation delegates to the .NET <c>SslStream</c> rather than implementing TLS.' Production has two ITlsProvider implementations: Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs:28 and Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:47, which runs the hand-built TLS client in Curl.Tls.UnitLibrary (Tls13ClientConnection, Tls12ClientConnection). Curl.Engine.UnitLibrary/EngineComposition.Transports.cs:62-63 constructs HandBuiltTlsProvider whenever TlsClientRouting.Choose returns HandBuilt. An agent reading the interface would believe Curl never implements TLS itself. Sampled type (seed 377386587). Phase 2: explained by ADR-0140, which added the hand-built second provider; the remark predates it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs -Pattern 'production implementation delegates'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.Transports.cs -Pattern 'new HandBuiltTlsProvider'
```

- Expected: The remark names both production providers (SslStreamTlsProvider and HandBuiltTlsProvider), or the engine constructs only an SslStream provider.
- Actual: ITlsProvider.cs:11: 'The production implementation delegates to the .NET' (single SslStream implementation claimed); EngineComposition.Transports.cs:63: ': new HandBuiltTlsProvider(options, timeProvider, sessions, echConfigs);'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
