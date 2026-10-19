---
id: BL-2804
title: Fix AF-0277: ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy checks five options, not ten
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2804 — Fix AF-0277: ProxyFromCommandLine_TheTenTlsOptionsOfAdr0151_NeverReachTheProxy checks five options, not ten

## Goal

The defect the audit office reported as AF-0277 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0277 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0277-proxyfromcommandline-thetentlsoptionsofadr0151-nev.md`.

Location: `Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs:251`

Location: `Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs:251`

The body is Assert.AreEqual(Expect(new TlsClientOptions()), MapProxy("--curves", "X25519", "--tls-earlydata", "--ech", "true", "--tlsuser", "user", "--tlspassword", "p", Url)). Only --curves, --tls-earlydata, --ech, --tlsuser and --tlspassword are given. The other five (for example --sigalgs, --ssl-sessions, --no-sessionid, --cert-status, --tlsauthtype) could leak into the proxy's TLS options and this test would still pass. Phase 2: ADR-0151's title names 'the ten TLS options'; the name promises all ten.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs -Pattern 'MapProxy\("--curves"'
```

- Expected: The test passes all ten ADR-0151 options to MapProxy.
- Actual: One MapProxy call naming five options: --curves, --tls-earlydata, --ech, --tlsuser, --tlspassword.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-19: The test now passes all ten ADR-0151 options (`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword`, `--tlsauthtype`) to `MapProxy`, using the values `FromCommandLine_TheSevenTlsOptionsOfAdr0151StillInUse_CopiesEachVerbatim` proves reach the target, and asserts the proxy gets default `TlsClientOptions`. Test-only change, so no quality run. Build clean; fast tests green (74/74 in the class, every project passed).

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
