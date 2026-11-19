---
id: AF-0883
title: ProxySelector docs list fewer schemes that read https_proxy/http_proxy than the code uses
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Core.UnitLibrary/ProxySelector.cs:ProxyVariableNames:false-doc-comment
reproduction: none
task: BL-5547
tasks: BL-5547
found: 2026-11-19
found-at: 20e558729f39eda4a4d373ccd10b22dd4ea46b2c
scorecard: 2026-11-19_0134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0883 - ProxySelector docs list fewer schemes that read https_proxy/http_proxy than the code uses

## Summary

Low finding from the truthfulness auditor at `Curl.Core.UnitLibrary/ProxySelector.cs:248`: ProxySelector docs list fewer schemes that read https_proxy/http_proxy than the code uses.

## Evidence

Location: `Curl.Core.UnitLibrary/ProxySelector.cs:248`

ProxyVariableNames summary (line 248): 'https_proxy then HTTPS_PROXY for wss and for the extension schemes that always speak HTTPS: oci, s3, gs, az and signalrs, ADR-1297'. The set it reads (line 243): 'HttpsProxySchemes = ["wss", "mcps", "grpcs", "socketios", "oci", "s3", "gs", "az", "abfs", "abfss", "signalrs"]', so mcps, grpcs, socketios, abfs and abfss read https_proxy too, unmentioned. The class remarks (lines 16-19) say only 'for ws http_proxy, for wss https_proxy and HTTPS_PROXY', with no extension scheme at all. ADR-1297 likewise says the rule 'is limited to these extension schemes' (the five).

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitLibrary/ProxySelector.cs -Pattern 'HttpsProxySchemes = |that always speak HTTPS: <c>oci</c>' | ForEach-Object { "$($_.LineNumber): $($_.Line.Trim())" }
```

- Expected: The summary names every scheme in HttpsProxySchemes (including mcps, grpcs, socketios, abfs, abfss).
- Actual: 243: the set holds 11 schemes; 248: the summary names wss plus oci, s3, gs, az and signalrs only.

## Re-audits

## Log

- 2026-11-19: filed proposed.
