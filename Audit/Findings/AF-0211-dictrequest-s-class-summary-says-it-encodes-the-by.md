---
id: AF-0211
title: DictRequest's class summary says it encodes the bytes curl 8.21.0 sends; its CLIENT line is libcurl 8.22.0
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:DictRequest:false-doc-comment
reproduction: none
task: BL-2213
tasks: BL-2213
found: 2026-10-14
found-at: c1dedd04f2a8e1534e64b664312c5dacc66bdf4a
scorecard: 2026-10-14_0453.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0211 - DictRequest's class summary says it encodes the bytes curl 8.21.0 sends; its CLIENT line is libcurl 8.22.0

## Summary

Low finding from the truthfulness auditor at `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:8`: DictRequest's class summary says it encodes the bytes curl 8.21.0 sends; its CLIENT line is libcurl 8.22.0.

## Evidence

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:8`

Line 8: '/// Encodes a <c>dict</c> URL's path as the bytes curl 8.21.0 sends: a <c>CLIENT</c> line, ...'. Line 37: public const string ClientLine = "CLIENT libcurl 8.22.0"; and line 34 says the bytes match curl 8.22.0 (ADR-0479). curl 8.21.0 sends CLIENT libcurl 8.21.0, so the encoded bytes are not what curl 8.21.0 sends. This is the same leftover AF-0201 fixed on ClientLine's own summary, here on the class summary.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'as the bytes curl 8\.21\.0 sends','ClientLine = '
```

- Expected: The class summary names the same curl version as ClientLine (8.22.0), or no 8.21.0 match.
- Actual: DictRequest.cs:8:/// Encodes a <c>dict</c> URL's path as the bytes curl 8.21.0 sends: a <c>CLIENT</c> line, | DictRequest.cs:37:    public const string ClientLine = "CLIENT libcurl 8.22.0";

## Re-audits

- 2026-10-14 | 2026-10-14_1815.md | reproduces: no | Ran the reproduction: 'as the bytes curl 8\.21\.0 sends' no longer matches. The summary at DictRequest.cs:8 reads 'as the bytes curl 8.22.0 sends', and line 37 is ClientLine = "CLIENT libcurl 8.22.0"; the two agree.

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> accepted.
