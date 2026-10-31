---
id: AF-0487
title: DohDnsResolver's forward-proxy target can flip proxy.Kind == ProxyKind.Https to != with no test failing, so DoH to an HTTPS proxy goes in plaintext
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DohDnsResolver.cs:ConnectTarget-eq:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DohDnsResolver.cs:160:==
task: BL-4397
tasks: BL-4397
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: mechanical
closed-by: 2026-10-31_1149.md
---
# AF-0487 - DohDnsResolver's forward-proxy target can flip proxy.Kind == ProxyKind.Https to != with no test failing, so DoH to an HTTPS proxy goes in plaintext

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/DohDnsResolver.cs:160`: DohDnsResolver's forward-proxy target can flip proxy.Kind == ProxyKind.Https to != with no test failing, so DoH to an HTTPS proxy goes in plaintext.

## Evidence

Location: `Curl.Networking.UnitLibrary/DohDnsResolver.cs:160`

Seed-0 sample of Curl.Networking.UnitLibrary: mutant `new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https) { IsForwardProxy = true, ... }` -> `proxy.Kind != ProxyKind.Https` survived. This is the target used when an http:// DoH server's POSTs are forwarded through an HTTP, HTTP/1.0 or HTTPS proxy (ForwardsThroughProxy). Under the mutant an https:// proxy gets no TLS and a plain http:// proxy gets a TLS handshake. That changes the request bytes on the wire and breaks every DoH lookup through either kind of proxy, and no test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohDnsResolver.cs:160:== -Member ConnectTarget -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the -Site command: 'killed Curl.Networking.UnitLibrary/DohDnsResolver.cs:160 ==' (resolvedLine 160, 'proxy.Kind == ProxyKind.Https' mutated to '!='); outcome killed. Baseline left out 3 HandBuiltTlsProviderTests ECH tests that fail unmutated. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
