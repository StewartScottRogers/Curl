---
id: BL-1889
title: Fix AF-0137: FeaturesLine doc says GSS-API, HTTP2, HTTP3 and TLS-SRP are listed on every platform; the Windows line drops all four
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1889 — Fix AF-0137: FeaturesLine doc says GSS-API, HTTP2, HTTP3 and TLS-SRP are listed on every platform; the Windows line drops all four

## Goal

The defect the audit office reported as AF-0137 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0137 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0137-featuresline-doc-says-gss-api-http2-http3-and-tls.md`.

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:38`

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:38`

Lines 37-44: '<c>HTTP2</c> on every platform now that <c>--http2</c> is accepted', '<c>GSS-API</c>, <c>Kerberos</c> and <c>SPNEGO</c> on every platform', '<c>HTTP3</c> on every platform', '<c>TLS-SRP</c> on every platform'. Line 64: WindowsFeaturesLine = 'Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd', with no GSS-API, HTTP2, HTTP3 or TLS-SRP, and Lines(isWindows: true) writes it (line 78). The drop follows ADR-0449 to ADR-0452, and WindowsFeaturesLine's own doc says so, so FeaturesLine's 'every platform' is stale.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Cli.UnitLibrary/CurlVersionText.cs -SimpleMatch 'on every platform now that').Count; Select-String -Path Curl.Cli.UnitLibrary/CurlVersionText.cs -Pattern 'public const string WindowsFeaturesLine = .*(HTTP2|HTTP3|GSS-API|TLS-SRP)'
```

- Expected: The FeaturesLine doc names the platforms each feature is on, consistent with WindowsFeaturesLine.
- Actual: 4 'on every platform now that' claims; WindowsFeaturesLine contains none of HTTP2, HTTP3, GSS-API or TLS-SRP (no match).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
