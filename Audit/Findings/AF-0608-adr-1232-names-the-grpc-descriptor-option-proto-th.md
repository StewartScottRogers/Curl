---
id: AF-0608
title: ADR-1232 names the gRPC descriptor option --proto; the option is --grpc-proto (--proto is curl's allowed-protocols option)
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces.
key: truthfulness:Documentation/Planning/Decisions/ADR-1232-protojson-any-resolves-its-type-in-the-catalogue.md:--proto:stale-adr
reproduction: none
task: BL-4772
tasks: BL-4772
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: reliable-reaudit
closed-by: 2026-11-03_1615.md
---
# AF-0608 - ADR-1232 names the gRPC descriptor option --proto; the option is --grpc-proto (--proto is curl's allowed-protocols option)

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1232-protojson-any-resolves-its-type-in-the-catalogue.md:20`: ADR-1232 names the gRPC descriptor option --proto; the option is --grpc-proto (--proto is curl's allowed-protocols option).

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1232-protojson-any-resolves-its-type-in-the-catalogue.md:20`

ADR-1232 Decision, line 20: "a gRPC call resolves it among the descriptors server reflection or `--proto` files loaded". The gRPC extension registers only "grpc-proto" (Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcExtension.cs:16), and every message names it as --grpc-proto (GrpcProtoMethod.cs:25,40,42; ServerReflectionClient.cs:51). ADR-1228 and ADR-1149 also say --grpc-proto. `--proto` is a different, real curl option (allowed protocols), so an agent following this ADR would pass the wrong option.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1232-*.md,Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcExtension.cs -Pattern '`--proto`|"grpc-proto"'
```

- Expected: The ADR names the option the extension registers, --grpc-proto; no `--proto` match in ADR-1232.
- Actual: ADR-1232-...md:20 matches `--proto`; GrpcExtension.cs:16 registers "grpc-proto".

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the reproduction: the only match is GrpcExtension.cs:16 '"grpc-proto",'; ADR-1232 no longer contains '`--proto`' and now names '`--grpc-proto` files' (line 20).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces.
