---
id: BL-4772
title: Fix AF-0608: ADR-1232 names the gRPC descriptor option --proto; the option is --grpc-proto (--proto is curl's allowed-protocols option)
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-03
completed:
---
# BL-4772 — Fix AF-0608: ADR-1232 names the gRPC descriptor option --proto; the option is --grpc-proto (--proto is curl's allowed-protocols option)

## Goal

The defect the audit office reported as AF-0608 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0608 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0608-adr-1232-names-the-grpc-descriptor-option-proto-th.md`.

Location: `Documentation/Planning/Decisions/ADR-1232-protojson-any-resolves-its-type-in-the-catalogue.md:20`

Location: `Documentation/Planning/Decisions/ADR-1232-protojson-any-resolves-its-type-in-the-catalogue.md:20`

ADR-1232 Decision, line 20: "a gRPC call resolves it among the descriptors server reflection or `--proto` files loaded". The gRPC extension registers only "grpc-proto" (Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcExtension.cs:16), and every message names it as --grpc-proto (GrpcProtoMethod.cs:25,40,42; ServerReflectionClient.cs:51). ADR-1228 and ADR-1149 also say --grpc-proto. `--proto` is a different, real curl option (allowed protocols), so an agent following this ADR would pass the wrong option.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1232-*.md,Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcExtension.cs -Pattern '`--proto`|"grpc-proto"'
```

- Expected: The ADR names the option the extension registers, --grpc-proto; no `--proto` match in ADR-1232.
- Actual: ADR-1232-...md:20 matches `--proto`; GrpcExtension.cs:16 registers "grpc-proto".

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
