---
id: BL-4331
title: Give certificate check hooks the port the checked connection was made to
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4331 — Give certificate check hooks the port the checked connection was made to

## Goal

`ICertificateCheckHook.RefusalOfAsync` receives the port of the connection whose chain it checks, as well as the host.

## Context

DANE (BL-4151) looks up TLSA records at `_<port>._tcp.<host>` (RFC 6698 section 3), but `Curl.Extension.Abstractions.UnitLibrary/Certificates/ICertificateCheckHook.cs` gives a check only the host. `CertificateCheckingConnector` has the `ConnectTarget` (host and port); `CertificateCheckingTlsProvider` (in-place STARTTLS upgrades) has only the plaintext `IConnection` and the target host. Decide, in an ADR marked "Decided by Claude under Stewart's delegation", how the provider path learns the port (for example from the plaintext connection's remote endpoint if `IConnection` exposes it, else a nullable port meaning "unknown") without changing `Curl.Protocol.Abstractions`; if it truly needs a contract change there, file that as its own task and requeue. Update `CertificateCheckHooks.CheckedAsync`, both wrappers, the interface's XML comments, the library's CLAUDE.md, and the hook stand-ins in `Curl.Extension.Abstractions.UnitTests/Certificates` and `Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs`. No extension implements the hook yet (grep `ICertificateCheckHook`), so nothing else changes.

## Acceptance criteria

- [ ] `ICertificateCheckHook.RefusalOfAsync` takes the port, and unit tests show the connector path passes `ConnectTarget.Port` and the TLS provider path passes the port the ADR decides.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the provider path's port.
- [ ] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29: the DANE check needs the port and the hook does not carry it.

## Log

- 2026-10-29: Created.
