---
id: BL-3129
title: Fix AF-0349: HttpProxyTunnelOptions.Default's doc says the User-Agent is curl/8.21.0; the code sets curl/8.22.0
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3129 — Fix AF-0349: HttpProxyTunnelOptions.Default's doc says the User-Agent is curl/8.21.0; the code sets curl/8.22.0

## Goal

The defect the audit office reported as AF-0349 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0349 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0349-httpproxytunneloptions-default-s-doc-says-the-user.md`.

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs:26`

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs:26`

Lines 25-26 say: 'Gets the options curl 8.21.0 uses when no option changes them: <c>User-Agent: curl/8.21.0</c> and UTF-8 credentials.' Line 28 is `public static HttpProxyTunnelOptions Default { get; } = new("curl/8.22.0", Encoding.UTF8);`. The default CONNECT User-Agent is curl/8.22.0, the version the rest of the solution targets, so the doc names the wrong default value and the wrong curl version.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/HttpProxyTunnelOptions.cs -Pattern 'curl/8\.2\d\.0' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: The doc comment and the constructor argument name the same User-Agent (curl/8.22.0).
- Actual: 26: /// curl/8.21.0</c> and UTF-8 credentials. / 28: public static HttpProxyTunnelOptions Default { get; } = new("curl/8.22.0", Encoding.UTF8);

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Doc comment now says curl 8.22.0 and User-Agent curl/8.22.0, matching the constructor argument. Build clean; Networking fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. Doc comment fixed
