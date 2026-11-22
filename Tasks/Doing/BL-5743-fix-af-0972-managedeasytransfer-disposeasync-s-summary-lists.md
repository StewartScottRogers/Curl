---
id: BL-5743
title: Fix AF-0972: ManagedEasyTransfer.DisposeAsync's summary lists its cleanup steps but omits that it writes the CURLOPT_ALTSVC file and closes the WebSocket
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5743 — Fix AF-0972: ManagedEasyTransfer.DisposeAsync's summary lists its cleanup steps but omits that it writes the CURLOPT_ALTSVC file and closes the WebSocket

## Goal

The defect the audit office reported as AF-0972 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0972 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs:85`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs:85`

The summary (lines 76-82) lists what curl_easy_cleanup does here: remove from the multi, hand HSTS to CURLOPT_HSTSWRITEFUNCTION, take the share's cookie lock and detach, close the connect-only connection and the handle's idle pooled connections. The body (lines 86-100) also runs 'await SaveAltSvcAsync()' (writes the handle's alt-svc cache to its CURLOPT_ALTSVC file), 'await SaveCookieJarAsync()' (writes the CURLOPT_COOKIEJAR file), and 'await (webSocket?.CloseAsync() ...)'. The two file writes are side effects a reader of the summary would not expect from it. The name is right (it mirrors curl_easy_cleanup); the summary leaves these out.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs -Pattern 'SaveAltSvcAsync\(\)\.Config|webSocket\?\.CloseAsync|CURLOPT_ALTSVC|cookie flush'
```

- Expected: The DisposeAsync summary names the alt-svc file write, the cookie-jar write and the WebSocket close alongside the steps it lists.
- Actual: Line 92 calls SaveAltSvcAsync() and line 98 webSocket?.CloseAsync(), but the summary at lines 76-82 mentions neither; only the private SaveAltSvcAsync's own doc (line 106) names CURLOPT_ALTSVC.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
