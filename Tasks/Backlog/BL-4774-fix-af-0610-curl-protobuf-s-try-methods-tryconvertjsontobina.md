---
id: BL-4774
title: Fix AF-0610: Curl.Protobuf's Try... methods (TryConvertJsonToBinary, TryDecode, TryRead and others) return an error enum, not bool; AF-0539 renamed only TryParse
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protobuf.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4774 — Fix AF-0610: Curl.Protobuf's Try... methods (TryConvertJsonToBinary, TryDecode, TryRead and others) return an error enum, not bool; AF-0539 renamed only TryParse

## Goal

The defect the audit office reported as AF-0610 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0610 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0610-curl-protobuf-s-try-methods-tryconvertjsontobinary.md`.

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonConverter.cs:10`

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonConverter.cs:10`

AF-0539's fix renamed ProtobufJsonParser.TryParse to Parse, but the same defect remains across the library. 45 'static Protobuf*Error Try*(' methods, including public ones: ProtobufJsonConverter.TryConvertJsonToBinary (line 10) and TryConvertBinaryToJson (line 22), which return ProtobufJsonError; DynamicMessageDecoder.TryDecode (DynamicMessages/DynamicMessageDecoder.cs:17), which returns ProtobufDecodeError; ProtobufJsonWrapperTypes/TimeTypes/StructTypes/FieldMask/Any.TryRead; ProtobufJsonScalars.TryRead/TryReadEnum/TryReadMapKey; ProtobufReader.TryReadPackedVarints/Fixed32/Fixed64. align-and-document: "A method named Try... returns bool". ADR-1228 itself calls TryConvertBinaryToJson by name.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem Curl.Protobuf.UnitLibrary -Recurse -Filter *.cs | Select-String -Pattern 'static Protobuf\w+Error Try\w+\(').Count
```

- Expected: 0: every Try... method returns bool, and methods returning an error enum are not named Try...
- Actual: 45

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
