---
id: AF-0612
title: ADR-1228 says wrappers, Struct, Any and FieldMask keep their plain message form; the code gives each its own ProtoJSON form
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-1228-protojson-timestamp-and-duration-are-strings.md:WellKnownTypesPlainForm:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0612 - ADR-1228 says wrappers, Struct, Any and FieldMask keep their plain message form; the code gives each its own ProtoJSON form

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1228-protojson-timestamp-and-duration-are-strings.md:32`: ADR-1228 says wrappers, Struct, Any and FieldMask keep their plain message form; the code gives each its own ProtoJSON form.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1228-protojson-timestamp-and-duration-are-strings.md:32`

ADR-1228 Consequences, line 32: "The other well-known types (wrappers, `Struct`, `Any`, `FieldMask`, `Empty`) keep their plain message form." The code now gives special forms to: ProtobufJsonWrapperTypes.cs ("The JSON forms of the nine wrapper types ... the wrapped value field's own JSON value"); ProtobufJsonStructTypes.cs ("The JSON forms of google.protobuf.Struct (any JSON object), google.protobuf.Value ..."); ProtobufJsonFieldMask.cs; and ProtobufJsonAny.cs (ADR-1232). Only Empty keeps its plain form. The ADR does not say it was superseded.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1228-*.md -Pattern 'plain message form'; Get-ChildItem Curl.Protobuf.UnitLibrary/Json -Filter 'ProtobufJson*.cs' | Select-Object -ExpandProperty Name
```

- Expected: ADR-1228 says only the types the code leaves alone (Empty) keep the plain form, or points to the later decisions.
- Actual: Line 32 lists wrappers, Struct, Any, FieldMask as plain; ProtobufJsonWrapperTypes.cs, ProtobufJsonStructTypes.cs, ProtobufJsonFieldMask.cs and ProtobufJsonAny.cs exist and give them their own forms.

## Re-audits

## Log

- 2026-11-03: filed proposed.
