---
id: AF-0137
title: FeaturesLine doc says GSS-API, HTTP2, HTTP3 and TLS-SRP are listed on every platform; the Windows line drops all four
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Cli.UnitLibrary/CurlVersionText.cs:FeaturesLine:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-09
found-at: 71f3acef7ec0d988d2d6b5d967a7b7300156cf44
scorecard: 2026-10-09_0647.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0137 - FeaturesLine doc says GSS-API, HTTP2, HTTP3 and TLS-SRP are listed on every platform; the Windows line drops all four

## Summary

Low finding from the truthfulness auditor at `Curl.Cli.UnitLibrary/CurlVersionText.cs:38`: FeaturesLine doc says GSS-API, HTTP2, HTTP3 and TLS-SRP are listed on every platform; the Windows line drops all four. Reported by an auditor flagged unreliable in 2026-10-09_0647.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/CurlVersionText.cs:38`

Lines 37-44: '<c>HTTP2</c> on every platform now that <c>--http2</c> is accepted', '<c>GSS-API</c>, <c>Kerberos</c> and <c>SPNEGO</c> on every platform', '<c>HTTP3</c> on every platform', '<c>TLS-SRP</c> on every platform'. Line 64: WindowsFeaturesLine = 'Features: alt-svc AsynchDNS brotli HSTS HTTPS-proxy HTTPSRR IDN IPv6 Kerberos Largefile libz NTLM PSL SPNEGO SSL SSPI threadsafe UnixSockets zstd', with no GSS-API, HTTP2, HTTP3 or TLS-SRP, and Lines(isWindows: true) writes it (line 78). The drop follows ADR-0449 to ADR-0452, and WindowsFeaturesLine's own doc says so, so FeaturesLine's 'every platform' is stale.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Cli.UnitLibrary/CurlVersionText.cs -SimpleMatch 'on every platform now that').Count; Select-String -Path Curl.Cli.UnitLibrary/CurlVersionText.cs -Pattern 'public const string WindowsFeaturesLine = .*(HTTP2|HTTP3|GSS-API|TLS-SRP)'
```

- Expected: The FeaturesLine doc names the platforms each feature is on, consistent with WindowsFeaturesLine.
- Actual: 4 'on every platform now that' claims; WindowsFeaturesLine contains none of HTTP2, HTTP3, GSS-API or TLS-SRP (no match).

## Re-audits

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
