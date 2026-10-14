---
id: AF-0201
title: DictRequest.ClientLine's summary says the bytes sent match curl 8.21.0; the constant is CLIENT libcurl 8.22.0
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:ClientLine:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0201 - DictRequest.ClientLine's summary says the bytes sent match curl 8.21.0; the constant is CLIENT libcurl 8.22.0

## Summary

Low finding from the truthfulness auditor at `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:33`: DictRequest.ClientLine's summary says the bytes sent match curl 8.21.0; the constant is CLIENT libcurl 8.22.0.

## Evidence

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:33`

The summary says: 'It names upstream curl's own version so the bytes sent match curl 8.21.0; it is one constant so it can follow the version this project measures against.' The constant directly below (line 37) is "CLIENT libcurl 8.22.0", which curl 8.21.0 would not send. The intent (follow the measured version) is right; the version named in it is stale. Phase 2: explained by ADR-0479 (BL-2127 moved the DICT client line to 8.22.0).

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'so the bytes sent match curl 8\.21\.0').Count; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: 0 (summary names the version the constant holds), or a constant naming 8.21.0
- Actual: 1
public const string ClientLine = "CLIENT libcurl 8.22.0";

## Re-audits

## Log

- 2026-10-13: filed proposed.
