---
id: AF-0433
title: ITransferHeaders.TryGet (and TransferHeaderStore.TryGet) returns a CurlHeaderCode, not a bool as a Try method should
auditor: truthfulness
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: truthfulness:Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs:TryGet:misleading-name
reproduction: none
task: BL-3498
tasks: BL-3498
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0433 - ITransferHeaders.TryGet (and TransferHeaderStore.TryGet) returns a CurlHeaderCode, not a bool as a Try method should

## Summary

Medium finding from the truthfulness auditor at `Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs:21`: ITransferHeaders.TryGet (and TransferHeaderStore.TryGet) returns a CurlHeaderCode, not a bool as a Try method should.

## Evidence

Location: `Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs:21`

ITransferHeaders.cs:21 declares 'CurlHeaderCode TryGet(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header);', and Curl.Engine.UnitLibrary/TransferHeaderStore.cs:107 implements it, returning CurlHeaderCode.Missing, BadIndex, the argument-check code or Ok. The naming rule in .claude/agents/align-and-document.md ('What aligned means') says a method named Try... returns bool. This one returns libcurl's CURLHcode result, so 'if (headers.TryGet(...))' does not compile, and a reader expects true/false where the method hands back curl_easy_header's code. A name such as Get or Find mirroring curl_easy_header would say what it does.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitLibrary/EngineContracts/Headers/ITransferHeaders.cs, Curl.Engine.UnitLibrary/TransferHeaderStore.cs -Pattern 'CurlHeaderCode TryGet\(' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: No match: a Try-named method returns bool, or the method returning CurlHeaderCode is not named Try...
- Actual: ITransferHeaders.cs:21 and TransferHeaderStore.cs:107

## Re-audits

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the reproduction: Select-String found no 'CurlHeaderCode TryGet(' in either file. ITransferHeaders now declares `CurlHeaderCode Find(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header)` and `CurlHeader? Next(...)`, and TransferHeaderStore implements Find (line 107) and Next (line 133). No Try-named member returns a non-bool.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
