---
id: BL-4163
title: Extension: Take credentials from the OS credential store
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092]
touches: [Curl.Extension.OsCredentialStore.UnitLibrary, Curl.Extension.OsCredentialStore.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4163 — Extension: Take credentials from the OS credential store

## Goal

Curl provides `--user-from-keychain <name>`: Windows Credential Manager, macOS Keychain, Linux libsecret, as the extension `Curl.Extension.OsCredentialStore.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 63 of 63. Specification: the three platforms' credential store APIs. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.OsCredentialStore.UnitLibrary` and `Curl.Extension.OsCredentialStore.UnitTests` exist in `/Extensions/OsCredentialStore/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--user-from-keychain <name>`: Windows Credential Manager, macOS Keychain, Linux libsecret.
- [x] Unit tests check its wire behaviour against named sections of the three platforms' credential store APIs, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan (ADR-1144): one transfer hook that reads the credential named by `--user-from-keychain` from the running OS's store and runs the transfer with it in place of `-u` (`CredentialedTransferContext`); none fails with exit 67 before connecting. Windows: `CredReadW` generic credential; macOS: `security find-generic-password -g -s`; Linux: `secret-tool search --unlock service`. Fit in one run, no split needed.
- Registration: the extension has no scheme, so as RecordReplay (ADR-1135) its lines are in the option, description and transfer hook catalogues of `Curl.Extension.Composition.UnitLibrary`, which are shared list files (`Tasks/SharedListFiles.txt`), not in `ExtensionCatalogue.cs`; touches unchanged for that reason.
- ADR-1136 to ADR-1138 were already taken on `origin/work/dark-factory`, so this ADR is ADR-1144.
- The two OS-touching adapters (`NativeWindowsCredentialReader`, `ProcessCommandRunner`) are excluded from coverage with ADR-0083 justifications; every other member is at 100% line and branch (Measure-CodeQuality: 0 failing members for the library, and for Curl.Extension.Composition.UnitLibrary).
- Measured on Windows: a `cmdkey /generic:` credential alice/s3cret was sent as `Authorization: Basic YWxpY2U6czNjcmV0` to a loopback server (the credential was deleted after); a missing name gave `curl: (67) No credential named ... in the Windows Credential Manager`; `CURL_EXTENSIONS=0` gave `option --user-from-keychain: is unknown`, exit 2.
- Platform: the tests use recorded tool output and fake `CredReadW` results only, no OS calls, so they are platform-neutral; the fast tests are green here (CI on Linux and macOS is left to the shift's CI watch).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c26bd2e0 92e286e6 9c4ce956 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --user-from-keychain <name> takes credentials from Windows Credential Manager, the macOS keychain or libsecret
