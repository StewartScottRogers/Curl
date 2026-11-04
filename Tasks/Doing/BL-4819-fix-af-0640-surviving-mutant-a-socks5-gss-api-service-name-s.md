---
id: BL-4819
title: Fix AF-0640: Surviving mutant: a SOCKS5 GSS-API service name starting with '/' can be used whole instead of naming the host, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4819 — Fix AF-0640: Surviving mutant: a SOCKS5 GSS-API service name starting with '/' can be used whole instead of naming the host, and no test fails

## Goal

The defect the audit office reported as AF-0640 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0640 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0640-surviving-mutant-a-socks5-gss-api-service-name-sta.md`.

Location: `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79`

Location: `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'slash < 0 ? (service, proxyHost) : (service[..slash], service[(slash + 1)..])' -> 'slash <= 0'. For --socks5-gssapi-service '/host.example' the Kerberos request uses '/host.example' as the service and the proxy host, instead of an empty service and host.example, so the requested SPN changes. No test passes a service name with a leading slash.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79:< -Member RequestFor -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
