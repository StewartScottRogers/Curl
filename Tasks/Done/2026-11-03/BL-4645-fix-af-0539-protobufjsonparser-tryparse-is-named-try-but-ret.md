---
id: BL-4645
title: Fix AF-0539: ProtobufJsonParser.TryParse is named Try... but returns a ProtobufJsonError enum, not bool
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protobuf.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-02
---
# BL-4645 — Fix AF-0539: ProtobufJsonParser.TryParse is named Try... but returns a ProtobufJsonError enum, not bool

## Goal

The defect the audit office reported as AF-0539 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0539 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0539-protobufjsonparser-tryparse-is-named-try-but-retur.md`.

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs:26`

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs:26`

Line 26: 'public static ProtobufJsonError TryParse(DynamicMessageType type, ReadOnlyMemory<byte> json, out DynamicMessage? message)'. Its summary says 'the message is null unless the error is ProtobufJsonError.None'. The solution's naming rule (align-and-document 'What aligned means': 'A method named Try... returns bool and does not throw for the expected failure') makes a reader expect a bool success flag. A caller writing 'if (ProtobufJsonParser.TryParse(...))' would not compile, and one comparing against true/false misreads the contract. Drawn in step 1 with seed 715403.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protobuf.UnitLibrary/Json/ProtobufJsonParser.cs -Pattern 'public static \w+ TryParse\('
```

- Expected: A TryParse that returns bool, or a method returning ProtobufJsonError whose name does not start with Try (for example Parse or ReadMessage).
- Actual: ProtobufJsonParser.cs:26:    public static ProtobufJsonError TryParse(DynamicMessageType type, ReadOnlyMemory<byte> json, out DynamicMessage? message)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Renamed ProtobufJsonParser.TryParse to Parse (returns ProtobufJsonError, so no Try prefix); updated ProtobufJsonConverter and the tests. Build clean, 327 Protobuf tests green, Measure-CodeQuality 0 failing members.

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. Renamed to Parse; build and tests green
