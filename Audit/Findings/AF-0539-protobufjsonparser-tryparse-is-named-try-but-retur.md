---
id: AF-0539
title: ProtobufJsonParser.TryParse is named Try... but returns a ProtobufJsonError enum, not bool
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs:ProtobufJsonParser.TryParse:misleading-name
reproduction: none
task: BL-4645
tasks: BL-4645
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_1017.md
---
# AF-0539 - ProtobufJsonParser.TryParse is named Try... but returns a ProtobufJsonError enum, not bool

## Summary

Medium finding from the truthfulness auditor at `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs:26`: ProtobufJsonParser.TryParse is named Try... but returns a ProtobufJsonError enum, not bool. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs:26`

Line 26: 'public static ProtobufJsonError TryParse(DynamicMessageType type, ReadOnlyMemory<byte> json, out DynamicMessage? message)'. Its summary says 'the message is null unless the error is ProtobufJsonError.None'. The solution's naming rule (align-and-document 'What aligned means': 'A method named Try... returns bool and does not throw for the expected failure') makes a reader expect a bool success flag. A caller writing 'if (ProtobufJsonParser.TryParse(...))' would not compile, and one comparing against true/false misreads the contract. Drawn in step 1 with seed 715403.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs -Pattern 'public static \w+ TryParse\('
```

- Expected: A TryParse that returns bool, or a method returning ProtobufJsonError whose name does not start with Try (for example Parse or ReadMessage).
- Actual: ProtobufJsonParser.cs:26:    public static ProtobufJsonError TryParse(DynamicMessageType type, ReadOnlyMemory<byte> json, out DynamicMessage? message)

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: no | Ran the reproduction: Select-String found no 'public static \w+ TryParse(' line in Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs.
- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Ran Select-String -Path Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs -Pattern 'public static \w+ TryParse\(': no match. Line 18 is now 'public static ProtobufJsonError Parse(...)'. Sibling Try... methods returning error enums remain and are filed separately (ProtobufJsonConverter.TryConvertJsonToBinary).

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces.
