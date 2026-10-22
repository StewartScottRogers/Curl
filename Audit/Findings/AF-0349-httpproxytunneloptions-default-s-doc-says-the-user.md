---
id: AF-0349
title: HttpProxyTunnelOptions.Default's doc says the User-Agent is curl/8.21.0; the code sets curl/8.22.0
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs:HttpProxyTunnelOptions.Default:false-doc-comment
reproduction: none
task: BL-3129
tasks: BL-3129
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0349 - HttpProxyTunnelOptions.Default's doc says the User-Agent is curl/8.21.0; the code sets curl/8.22.0

## Summary

Medium finding from the truthfulness auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs:26`: HttpProxyTunnelOptions.Default's doc says the User-Agent is curl/8.21.0; the code sets curl/8.22.0.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs:26`

Lines 25-26 say: 'Gets the options curl 8.21.0 uses when no option changes them: <c>User-Agent: curl/8.21.0</c> and UTF-8 credentials.' Line 28 is `public static HttpProxyTunnelOptions Default { get; } = new("curl/8.22.0", Encoding.UTF8);`. The default CONNECT User-Agent is curl/8.22.0, the version the rest of the solution targets, so the doc names the wrong default value and the wrong curl version.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs -Pattern 'curl/8\.2\d\.0' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: The doc comment and the constructor argument name the same User-Agent (curl/8.22.0).
- Actual: 26: /// curl/8.21.0</c> and UTF-8 credentials. / 28: public static HttpProxyTunnelOptions Default { get; } = new("curl/8.22.0", Encoding.UTF8);

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
