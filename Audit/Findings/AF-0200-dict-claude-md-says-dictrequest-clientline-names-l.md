---
id: AF-0200
title: Dict CLAUDE.md says DictRequest.ClientLine names libcurl 8.21.0; it is CLIENT libcurl 8.22.0
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Protocol.Dict.UnitLibrary/CLAUDE.md:ClientLine:false-statement
reproduction: none
task: BL-2193
tasks: BL-2193
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed: 2026-10-14
closed-how: reliable-reaudit
closed-by: 2026-10-14_0453.md
---
# AF-0200 - Dict CLAUDE.md says DictRequest.ClientLine names libcurl 8.21.0; it is CLIENT libcurl 8.22.0

## Summary

High finding from the truthfulness auditor at `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md:27`: Dict CLAUDE.md says DictRequest.ClientLine names libcurl 8.21.0; it is CLIENT libcurl 8.22.0.

## Evidence

Location: `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md:27`

The Layout section says: 'Its `ClientLine` constant names `libcurl 8.21.0`, as upstream sends it.' Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:37 is 'public const string ClientLine = "CLIENT libcurl 8.22.0";', and every Dict test expects 'CLIENT libcurl 8.22.0\r\n' (DictProtocolHandlerTests.cs:16, DictProtocolHandlerTransferEventsTests.cs:26-28, among others). The file also says 'Every byte these classes send was measured against curl 8.21.0', which no longer holds for the CLIENT line. Phase 2: explained by ADR-0479 (decision 3.2 / BL-2127: DICT reports 8.22.0). Decision 4 keeps measurement statements, but this sentence states the constant's current value.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Dict.UnitLibrary/CLAUDE.md -Pattern 'names .libcurl 8\.21\.0').Count; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: 1, then public const string ClientLine = "CLIENT libcurl 8.21.0"; (or 0 when the CLAUDE.md names 8.22.0)
- Actual: 1
public const string ClientLine = "CLIENT libcurl 8.22.0";

## Re-audits

- 2026-10-14 | 2026-10-14_0453.md | reproduces: no | Ran the reproduction: 0 matches for 'names .libcurl 8\.21\.0' in Curl.Protocol.Dict.UnitLibrary/CLAUDE.md. Line 27 now says the ClientLine constant is 'CLIENT libcurl 8.22.0', matching 'public const string ClientLine = "CLIENT libcurl 8.22.0";'.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
- 2026-10-14: accepted -> closed. Re-audit 2026-10-14_0453.md: the reproduction no longer reproduces.
