---
id: AF-0320
title: ITlsProvider's remarks say the production implementation delegates to SslStream rather than implementing TLS; a second, hand-built production provider exists
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs:ITlsProvider:false-doc-comment
reproduction: none
task: BL-3073
tasks: BL-3073
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_1120.md
---
# AF-0320 - ITlsProvider's remarks say the production implementation delegates to SslStream rather than implementing TLS; a second, hand-built production provider exists

## Summary

Medium finding from the truthfulness auditor at `Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs:11`: ITlsProvider's remarks say the production implementation delegates to SslStream rather than implementing TLS; a second, hand-built production provider exists. Reported by an auditor flagged unreliable in 2026-10-22_0130.md.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs:11`

Remarks, lines 11-12: 'The production implementation delegates to the .NET <c>SslStream</c> rather than implementing TLS.' Production has two ITlsProvider implementations: Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs:28 and Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:47, which runs the hand-built TLS client in Curl.Tls.UnitLibrary (Tls13ClientConnection, Tls12ClientConnection). Curl.Engine.UnitLibrary/EngineComposition.Transports.cs:62-63 constructs HandBuiltTlsProvider whenever TlsClientRouting.Choose returns HandBuilt. An agent reading the interface would believe Curl never implements TLS itself. Sampled type (seed 377386587). Phase 2: explained by ADR-0140, which added the hand-built second provider; the remark predates it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs -Pattern 'production implementation delegates'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.Transports.cs -Pattern 'new HandBuiltTlsProvider'
```

- Expected: The remark names both production providers (SslStreamTlsProvider and HandBuiltTlsProvider), or the engine constructs only an SslStream provider.
- Actual: ITlsProvider.cs:11: 'The production implementation delegates to the .NET' (single SslStream implementation claimed); EngineComposition.Transports.cs:63: ': new HandBuiltTlsProvider(options, timeProvider, sessions, echConfigs);'

## Re-audits

- 2026-10-22 | 2026-10-22_1120.md | reproduces: no | Ran the reproduction: 'production implementation delegates' no longer appears in ITlsProvider.cs. The remarks now say 'Production has two providers: SslStreamTlsProvider ... and HandBuiltTlsProvider ...', which matches EngineComposition.Transports.cs:63 'new HandBuiltTlsProvider(...)'.

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_1120.md: the reproduction no longer reproduces.
