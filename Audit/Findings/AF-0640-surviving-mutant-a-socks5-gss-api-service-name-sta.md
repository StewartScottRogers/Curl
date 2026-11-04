---
id: AF-0640
title: Surviving mutant: a SOCKS5 GSS-API service name starting with '/' can be used whole instead of naming the host, and no test fails
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:RequestFor-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79:<
task: BL-4819
tasks: BL-4819
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_0643.md
---
# AF-0640 - Surviving mutant: a SOCKS5 GSS-API service name starting with '/' can be used whole instead of naming the host, and no test fails

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79`: Surviving mutant: a SOCKS5 GSS-API service name starting with '/' can be used whole instead of naming the host, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'slash < 0 ? (service, proxyHost) : (service[..slash], service[(slash + 1)..])' -> 'slash <= 0'. For --socks5-gssapi-service '/host.example' the Kerberos request uses '/host.example' as the service and the proxy host, instead of an empty service and host.example, so the requested SPN changes. No test passes a service name with a leading slash.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/Socks5GssapiNegotiation.cs:79:< -Member RequestFor -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-04 | 2026-11-04_0643.md | reproduces: no | Ran the -Site command: outcome killed, resolvedLine 79. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
