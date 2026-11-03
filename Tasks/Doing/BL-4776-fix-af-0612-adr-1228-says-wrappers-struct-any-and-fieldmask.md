---
id: BL-4776
title: Fix AF-0612: ADR-1228 says wrappers, Struct, Any and FieldMask keep their plain message form; the code gives each its own ProtoJSON form
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-03
completed:
---
# BL-4776 — Fix AF-0612: ADR-1228 says wrappers, Struct, Any and FieldMask keep their plain message form; the code gives each its own ProtoJSON form

## Goal

The defect the audit office reported as AF-0612 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0612 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0612-adr-1228-says-wrappers-struct-any-and-fieldmask-ke.md`.

Location: `Documentation/Planning/Decisions/ADR-1228-protojson-timestamp-and-duration-are-strings.md:32`

Location: `Documentation/Planning/Decisions/ADR-1228-protojson-timestamp-and-duration-are-strings.md:32`

ADR-1228 Consequences, line 32: "The other well-known types (wrappers, `Struct`, `Any`, `FieldMask`, `Empty`) keep their plain message form." The code now gives special forms to: ProtobufJsonWrapperTypes.cs ("The JSON forms of the nine wrapper types ... the wrapped value field's own JSON value"); ProtobufJsonStructTypes.cs ("The JSON forms of google.protobuf.Struct (any JSON object), google.protobuf.Value ..."); ProtobufJsonFieldMask.cs; and ProtobufJsonAny.cs (ADR-1232). Only Empty keeps its plain form. The ADR does not say it was superseded.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1228-*.md -Pattern 'plain message form'; Get-ChildItem Curl.Protobuf.UnitLibrary/Json -Filter 'ProtobufJson*.cs' | Select-Object -ExpandProperty Name
```

- Expected: ADR-1228 says only the types the code leaves alone (Empty) keep the plain form, or points to the later decisions.
- Actual: Line 32 lists wrappers, Struct, Any, FieldMask as plain; ProtobufJsonWrapperTypes.cs, ProtobufJsonStructTypes.cs, ProtobufJsonFieldMask.cs and ProtobufJsonAny.cs exist and give them their own forms.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
