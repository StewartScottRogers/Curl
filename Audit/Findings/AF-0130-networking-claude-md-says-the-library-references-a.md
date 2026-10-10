---
id: AF-0130
title: Networking CLAUDE.md says the library references Abstractions, Tls, Quic and Kerberos and nothing else, but it also references Curl.Http2.UnitLibrary
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
key: truthfulness:Curl.Networking.UnitLibrary/CLAUDE.md:ProjectReferences:false-statement
reproduction: none
task: BL-1880
tasks: BL-1880
found: 2026-10-09
found-at: 64e750b3931ea71536d942080142f37bfc4c9ccf
scorecard: 2026-10-09_0225.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0647.md, 2026-10-09_1435.md
---
# AF-0130 - Networking CLAUDE.md says the library references Abstractions, Tls, Quic and Kerberos and nothing else, but it also references Curl.Http2.UnitLibrary

## Summary

High finding from the truthfulness auditor at `Curl.Networking.UnitLibrary/CLAUDE.md:9`: Networking CLAUDE.md says the library references Abstractions, Tls, Quic and Kerberos and nothing else, but it also references Curl.Http2.UnitLibrary. Reported by an auditor flagged unreliable in 2026-10-09_0225.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/CLAUDE.md:9`

Curl.Networking.UnitLibrary/CLAUDE.md lines 6-9: 'It references that project, `Curl.Tls.UnitLibrary` ..., `Curl.Quic.UnitLibrary` ..., and `Curl.Kerberos.UnitLibrary`, whose KDC transport and SRV lookup it implements, and nothing else'. Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj:9 has <ProjectReference Include="..\Curl.Http2.UnitLibrary\Curl.Http2.UnitLibrary.csproj" />, and Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs has 'using Curl.Http2'. The word Http2 does not appear anywhere in the CLAUDE.md. This rule tells agents what the project may reference, and it is false: an agent that follows it would remove or refuse the Http2 reference and break the h2 proxy tunnel. Phase 2 annotation: git log shows the reference arrived in commit b2988cd82 (2026-10-03, 'tunnel through an h2 HTTPS proxy over an HTTP/2 CONNECT stream'), and the CLAUDE.md was not updated with it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj -SimpleMatch 'Curl.Http2.UnitLibrary'; (Select-String -Path Curl.Networking.UnitLibrary/CLAUDE.md -SimpleMatch 'Http2').Count
```

- Expected: Either no Curl.Http2.UnitLibrary reference in the csproj, or a CLAUDE.md that names it (count greater than 0).
- Actual: Curl.Networking.UnitLibrary.csproj:9: <ProjectReference Include="..\Curl.Http2.UnitLibrary\Curl.Http2.UnitLibrary.csproj" />, then 0

## Re-audits

- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the reproduction: the csproj still references Curl.Http2.UnitLibrary (line 9), and (Select-String Curl.Networking.UnitLibrary/CLAUDE.md -SimpleMatch 'Http2').Count is now 2. Lines 7-10 list the Http2 reference (for Http2ProxyTunnelConnection) before 'and nothing else'.
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction: the csproj still references Curl.Http2.UnitLibrary (line 9), and (Select-String Curl.Networking.UnitLibrary/CLAUDE.md -SimpleMatch 'Http2').Count is 2. CLAUDE.md line 9 now names `Curl.Http2.UnitLibrary`, whose Http2Connection carries the CONNECT stream of an h2 HTTPS proxy tunnel, before 'and nothing else'.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
