---
id: BL-5547
title: Fix AF-0883: ProxySelector docs list fewer schemes that read https_proxy/http_proxy than the code uses
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5547 — Fix AF-0883: ProxySelector docs list fewer schemes that read https_proxy/http_proxy than the code uses

## Goal

The defect the audit office reported as AF-0883 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0883 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Core.UnitLibrary/ProxySelector.cs:248`

Location: `Curl.Core.UnitLibrary/ProxySelector.cs:248`

ProxyVariableNames summary (line 248): 'https_proxy then HTTPS_PROXY for wss and for the extension schemes that always speak HTTPS: oci, s3, gs, az and signalrs, ADR-1297'. The set it reads (line 243): 'HttpsProxySchemes = ["wss", "mcps", "grpcs", "socketios", "oci", "s3", "gs", "az", "abfs", "abfss", "signalrs"]', so mcps, grpcs, socketios, abfs and abfss read https_proxy too, unmentioned. The class remarks (lines 16-19) say only 'for ws http_proxy, for wss https_proxy and HTTPS_PROXY', with no extension scheme at all. ADR-1297 likewise says the rule 'is limited to these extension schemes' (the five).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitLibrary/ProxySelector.cs -Pattern 'HttpsProxySchemes = |that always speak HTTPS: <c>oci</c>' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: The summary names every scheme in HttpsProxySchemes (including mcps, grpcs, socketios, abfs, abfss).
- Actual: 243: the set holds 11 schemes; 248: the summary names wss plus oci, s3, gs, az and signalrs only.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
