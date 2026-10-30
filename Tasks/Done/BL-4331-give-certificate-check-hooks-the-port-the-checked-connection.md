---
id: BL-4331
title: Give certificate check hooks the port the checked connection was made to
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Certificates, Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs, Documentation/Planning/Decisions/README.md, Curl.Extension.CertificateTransparency.UnitLibrary/CertificateTransparencyCheckHook.cs, Curl.Extension.CertificateTransparency.UnitTests/CertificateTransparencyCheckHookTests.cs, Curl.Extension.CertificateTransparency.UnitTests/CertificateTransparencyExtensionTests.cs, Curl.Extension.MlDsaCertificates.UnitLibrary/MlDsaCertificateCheckHook.cs, Curl.Extension.MlDsaCertificates.UnitTests/MlDsaCertificateCheckHookTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4331 — Give certificate check hooks the port the checked connection was made to

## Goal

`ICertificateCheckHook.RefusalOfAsync` receives the port of the connection whose chain it checks, as well as the host.

## Context

DANE (BL-4151) looks up TLSA records at `_<port>._tcp.<host>` (RFC 6698 section 3), but `Curl.Extension.Abstractions.UnitLibrary/Certificates/ICertificateCheckHook.cs` gives a check only the host. `CertificateCheckingConnector` has the `ConnectTarget` (host and port); `CertificateCheckingTlsProvider` (in-place STARTTLS upgrades) has only the plaintext `IConnection` and the target host. Decide, in an ADR marked "Decided by Claude under Stewart's delegation", how the provider path learns the port (for example from the plaintext connection's remote endpoint if `IConnection` exposes it, else a nullable port meaning "unknown") without changing `Curl.Protocol.Abstractions`; if it truly needs a contract change there, file that as its own task and requeue. Update `CertificateCheckHooks.CheckedAsync`, both wrappers, the interface's XML comments, the library's CLAUDE.md, and the hook stand-ins in `Curl.Extension.Abstractions.UnitTests/Certificates` and `Curl.Console.UnitTests/ExtensionCertificateCheckHookTests.cs`. No extension implements the hook yet (grep `ICertificateCheckHook`), so nothing else changes.

## Acceptance criteria

- [x] `ICertificateCheckHook.RefusalOfAsync` takes the port, and unit tests show the connector path passes `ConnectTarget.Port` and the TLS provider path passes the port the ADR decides.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the provider path's port.
- [x] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Filed by BL-4151's run on 2026-10-29: the DANE check needs the port and the hook does not carry it.
- 2026-10-30 (lane 3): two extensions now implement the hook (Certificate Transparency BL-4152, ML-DSA certificates), so their hook and test files were added to touches; no task in Doing on origin/work/dark-factory names them. They take the new parameter and ignore it.
- Decision (ADR-1131): the hook takes int? port. The connector path passes ConnectTarget.Port; CertificateCheckHooks keeps each connection the checking connector returned in a ConditionalWeakTable with its target, and the TLS provider path passes that target's port for the plaintext connection, or null when the checking connector did not open it. The plaintext RemoteEndPoint was rejected: through a proxy tunnel it is the proxy's port.
- Pipeline feature run directly: the change is one signature and one lookup in one library, so no architect stage was needed.
- Verified: dotnet build clean (0 warnings); fast tests green in all 100 test projects; Measure-CodeQuality.ps1 for Abstractions, CertificateTransparency and MlDsaCertificates: 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: aa0b566e c0b425b0 e7c4212f 3d836d5e.
- 2026-10-30: Doing -> Done. Certificate check hooks get the port: ConnectTarget.Port on connect, the checking connector's remembered port (or null) on STARTTLS (ADR-1131)
