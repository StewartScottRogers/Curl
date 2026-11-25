---
id: AF-0972
title: ManagedEasyTransfer.DisposeAsync's summary lists its cleanup steps but omits that it writes the CURLOPT_ALTSVC file and closes the WebSocket
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs:ManagedEasyTransfer.DisposeAsync:false-doc-comment
reproduction: none
task: BL-5743
tasks: BL-5743
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
---
# AF-0972 - ManagedEasyTransfer.DisposeAsync's summary lists its cleanup steps but omits that it writes the CURLOPT_ALTSVC file and closes the WebSocket

## Summary

Low finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs:85`: ManagedEasyTransfer.DisposeAsync's summary lists its cleanup steps but omits that it writes the CURLOPT_ALTSVC file and closes the WebSocket. Reported by an auditor flagged unreliable in 2026-11-22_0210.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs:85`

The summary (lines 76-82) lists what curl_easy_cleanup does here: remove from the multi, hand HSTS to CURLOPT_HSTSWRITEFUNCTION, take the share's cookie lock and detach, close the connect-only connection and the handle's idle pooled connections. The body (lines 86-100) also runs 'await SaveAltSvcAsync()' (writes the handle's alt-svc cache to its CURLOPT_ALTSVC file), 'await SaveCookieJarAsync()' (writes the CURLOPT_COOKIEJAR file), and 'await (webSocket?.CloseAsync() ...)'. The two file writes are side effects a reader of the summary would not expect from it. The name is right (it mirrors curl_easy_cleanup); the summary leaves these out.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs -Pattern 'SaveAltSvcAsync\(\)\.Config|webSocket\?\.CloseAsync|CURLOPT_ALTSVC|cookie flush'
```

- Expected: The DisposeAsync summary names the alt-svc file write, the cookie-jar write and the WebSocket close alongside the steps it lists.
- Actual: Line 92 calls SaveAltSvcAsync() and line 98 webSocket?.CloseAsync(), but the summary at lines 76-82 mentions neither; only the private SaveAltSvcAsync's own doc (line 106) names CURLOPT_ALTSVC.

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the reproduction: the DisposeAsync summary (ManagedEasyTransfer.Lifecycle.cs:80-83) now says it 'writes the handle's alt-svc cache to its CURLOPT_ALTSVC file (SaveAltSvcAsync)' and closes 'the handle's WebSocket connection, if any'. That matches line 95 'await SaveAltSvcAsync()' and line 100 'webSocket?.CloseAsync()'.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
