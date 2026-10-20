---
id: AF-0277
title: ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy checks five options, not ten
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: quality:Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs:ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy:name-lies
reproduction: none
task: BL-2804
tasks: BL-2804
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
---
# AF-0277 - ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy checks five options, not ten

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs:251`: ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy checks five options, not ten. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs:251`

The body is Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--curves", "X25519", "--tls-earlydata", "--ech", "true", "--tlsuser", "user", "--tlspassword", "p", Url)). Only --curves, --tls-earlydata, --ech, --tlsuser and --tlspassword are given. The other five (for example --sigalgs, --ssl-sessions, --no-sessionid, --cert-status, --tlsauthtype) could leak into the proxy's TLS options and this test would still pass. Phase 2: ADR-0151's title names 'the ten TLS options'; the name promises all ten.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs -Pattern 'MapProxy\("--curves"'
```

- Expected: The test passes all ten ADR-0151 options to MapProxy.
- Actual: One MapProxy call naming five options: --curves, --tls-earlydata, --ech, --tlsuser, --tlspassword.

## Re-audits

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Select-String still matches the MapProxy("--curves" call (TlsClientOptionsMappingTests.cs:298), but the call now passes ten options: --curves, --sigalgs, --tls-earlydata, --ech, --ssl-sessions, --engine, --dump-ca-embed, --tlsuser, --tlspassword, --tlsauthtype. The test checks the ten its name promises.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
