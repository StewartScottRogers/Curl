---
id: BL-4397
title: Fix AF-0487: DohDnsResolver's forward-proxy target can flip proxy.Kind == ProxyKind.Https to != with no test failing, so DoH to an HTTPS proxy goes in plaintext
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/DohDnsResolverTests.cs]
requirement: none
created: 2026-10-31
completed: 2026-10-31
---
# BL-4397 — Fix AF-0487: DohDnsResolver's forward-proxy target can flip proxy.Kind == ProxyKind.Https to != with no test failing, so DoH to an HTTPS proxy goes in plaintext

## Goal

The defect the audit office reported as AF-0487 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0487 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0487-dohdnsresolver-s-forward-proxy-target-can-flip-pro.md`.

Location: `Curl.Networking.UnitLibrary/DohDnsResolver.cs:160`

Location: `Curl.Networking.UnitLibrary/DohDnsResolver.cs:160`

Seed-0 sample of Curl.Networking.UnitLibrary: mutant `new ConnectTarget(proxy.Host, proxy.Port, proxy.Kind == ProxyKind.Https) { IsForwardProxy = true, ... }` -> `proxy.Kind != ProxyKind.Https` survived. This is the target used when an http:// DoH server's POSTs are forwarded through an HTTP, HTTP/1.0 or HTTPS proxy (ForwardsThroughProxy). Under the mutant an https:// proxy gets no TLS and a plain http:// proxy gets a TLS handshake. That changes the request bytes on the wire and breaks every DoH lookup through either kind of proxy, and no test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DohDnsResolver.cs:160:== -Member ConnectTarget -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Curl.Networking.UnitTests/DohDnsResolverTests.cs to touches: the fix is a test there (no task in Doing on origin/work/dark-factory names Curl.Networking). No production change was needed: the code is right, only the test was missing.
- New data-row test ResolveAsync_WithAnHttpDohServerThroughAForwardProxy_ConnectsToTheProxyWithTlsOnlyForAnHttpsProxy pins the whole forward-proxy ConnectTarget (TLS on for Https, off for Http and Http10, IsForwardProxy, PoolScheme) for an http:// DoH server.
- Mutant check: lanes may not run the audit office mutation tool, so I applied the same mutant (== to != at DohDnsResolver.cs:160) by hand. All 3 rows failed under it and pass without it, so the mutant is killed. The re-audit runs the finding command itself.
- dotnet build clean (0 warnings); the fast tests (TestCategory!=Integration) had no failures on Windows. The test is platform-neutral (fake connector, no paths or certificates).

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-31: Doing -> Done. DoH forward-proxy target's TLS choice is pinned by a test that kills the AF-0487 mutant
