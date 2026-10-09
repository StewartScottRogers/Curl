---
id: BL-1880
title: Fix AF-0130: Networking CLAUDE.md says the library references Abstractions, Tls, Quic and Kerberos and nothing else, but it also references Curl.Http2.UnitLibrary
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1880 — Fix AF-0130: Networking CLAUDE.md says the library references Abstractions, Tls, Quic and Kerberos and nothing else, but it also references Curl.Http2.UnitLibrary

## Goal

The defect the audit office reported as AF-0130 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0130 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0130-networking-claude-md-says-the-library-references-a.md`.

Location: `Curl.Networking.UnitLibrary/CLAUDE.md:9`

Location: `Curl.Networking.UnitLibrary/CLAUDE.md:9`

Curl.Networking.UnitLibrary/CLAUDE.md lines 6-9: 'It references that project, `Curl.Tls.UnitLibrary` ..., `Curl.Quic.UnitLibrary` ..., and `Curl.Kerberos.UnitLibrary`, whose KDC transport and SRV lookup it implements, and nothing else'. Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj:9 has <ProjectReference Include="..\Curl.Http2.UnitLibrary\Curl.Http2.UnitLibrary.csproj" />, and Curl.Networking.UnitLibrary/Http2ProxyTunnelConnection.cs has 'using Curl.Http2'. The word Http2 does not appear anywhere in the CLAUDE.md. This rule tells agents what the project may reference, and it is false: an agent that follows it would remove or refuse the Http2 reference and break the h2 proxy tunnel. Phase 2 annotation: git log shows the reference arrived in commit b2988cd82 (2026-10-03, 'tunnel through an h2 HTTPS proxy over an HTTP/2 CONNECT stream'), and the CLAUDE.md was not updated with it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/Curl.Networking.UnitLibrary.csproj -SimpleMatch 'Curl.Http2.UnitLibrary'; (Select-String -Path Curl.Networking.UnitLibrary/CLAUDE.md -SimpleMatch 'Http2').Count
```

- Expected: Either no Curl.Http2.UnitLibrary reference in the csproj, or a CLAUDE.md that names it (count greater than 0).
- Actual: Curl.Networking.UnitLibrary.csproj:9: <ProjectReference Include="..\Curl.Http2.UnitLibrary\Curl.Http2.UnitLibrary.csproj" />, then 0

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
