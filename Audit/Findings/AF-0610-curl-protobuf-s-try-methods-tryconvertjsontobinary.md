---
id: AF-0610
title: Curl.Protobuf's Try... methods (TryConvertJsonToBinary, TryDecode, TryRead and others) return an error enum, not bool; AF-0539 renamed only TryParse
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Protobuf.UnitLibrary/Json/ProtobufJsonConverter.cs:TryConvertJsonToBinary:misleading-name
reproduction: none
task: BL-4774
tasks: BL-4774
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0610 - Curl.Protobuf's Try... methods (TryConvertJsonToBinary, TryDecode, TryRead and others) return an error enum, not bool; AF-0539 renamed only TryParse

## Summary

Medium finding from the truthfulness auditor at `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonConverter.cs:10`: Curl.Protobuf's Try... methods (TryConvertJsonToBinary, TryDecode, TryRead and others) return an error enum, not bool; AF-0539 renamed only TryParse.

## Evidence

Location: `Curl.Protobuf.UnitLibrary/Json/ProtobufJsonConverter.cs:10`

AF-0539's fix renamed ProtobufJsonParser.TryParse to Parse, but the same defect remains across the library. 45 'static Protobuf*Error Try*(' methods, including public ones: ProtobufJsonConverter.TryConvertJsonToBinary (line 10) and TryConvertBinaryToJson (line 22), which return ProtobufJsonError; DynamicMessageDecoder.TryDecode (DynamicMessages/DynamicMessageDecoder.cs:17), which returns ProtobufDecodeError; ProtobufJsonWrapperTypes/TimeTypes/StructTypes/FieldMask/Any.TryRead; ProtobufJsonScalars.TryRead/TryReadEnum/TryReadMapKey; ProtobufReader.TryReadPackedVarints/Fixed32/Fixed64. align-and-document: "A method named Try... returns bool". ADR-1228 itself calls TryConvertBinaryToJson by name.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem Curl.Protobuf.UnitLibrary -Recurse -Filter *.cs | Select-String -Pattern 'static Protobuf\w+Error Try\w+\(').Count
```

- Expected: 0: every Try... method returns bool, and methods returning an error enum are not named Try...
- Actual: 45

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
