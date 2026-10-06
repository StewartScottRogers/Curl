---
id: BL-1410
title: Cover NtlmHttpAuthenticator.ContextRequestFor and SystemSecurityContext.Step to 100% branch when Curl.Authentication is measured on Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1410 — Cover NtlmHttpAuthenticator.ContextRequestFor and SystemSecurityContext.Step to 100% branch when Curl.Authentication is measured on Windows

## Goal

`powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary`, run on Windows as every dark factory lane runs it, reports no failing member: `NtlmHttpAuthenticator.ContextRequestFor` and `SystemSecurityContext.Step` reach 100% branch coverage with tests that run on Windows.

## Context

- BL-1303 (2026-10-03) and BL-1336 (2026-10-03) both measured `Curl.Authentication.UnitLibrary` on Windows at 100% line and 99.68% branch with two failing members they did not change: `NtlmHttpAuthenticator.ContextRequestFor` (80% branch) and `SystemSecurityContext.Step` (87.5% branch). Their Notes say the missing branches are taken only by tests marked for Linux (`Curl.Authentication.UnitTests/SystemSecurityContextFactoryTests.cs` has `[OSCondition(OperatingSystems.Linux)]` at line 128 beside its Windows-only tests), so a Windows lane always sees two failing members and every later task in this library has to explain them away.
- `Curl.Authentication.UnitLibrary/SystemSecurityContext.cs` `Step`: `authentication ??= new NegotiateAuthentication(options)`, then `token ?? []` only for `ContinueNeeded`/`Completed`, and a `Win32Exception` catch; the BCL `NegotiateAuthentication` is constructed inside, so a test cannot choose its answer. `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs` `ContextRequestFor` (around line 118) chooses between the default credential, an explicit `DOMAIN\user` split by `NtlmUserName.SplitDomain`, and the credential's own `Domain`.
- The root `CLAUDE.md` quality gates: 100% line and branch for every `*.UnitLibrary`; thresholds are not to be changed. Tests must pass on Windows, Linux and macOS.

## Acceptance criteria

- [x] The coverage report of `dotnet test Curl.Authentication.UnitTests --collect "Code Coverage;Format=cobertura"` on Windows shows every branch of `ContextRequestFor` and `Step` covered, by tests with no `OSCondition` (or a Windows one) that use no real SSPI credential, KDC or network.
- [x] Where `Step` needs a seam to make the BCL context's answer choosable (for example a factory for the `NegotiateAuthentication` it builds), the seam is internal, keeps `SystemSecurityContext`'s behaviour on both platforms unchanged, and the existing Linux and Windows tests pass unchanged.
- [x] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean; `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` on Windows reports no failing member.

## Notes

- If a branch truly cannot occur on Windows (the BCL never returns that status there), say so with the evidence and restructure so the branch does not exist on the Windows path, rather than leaving it uncovered.
- Measured first (Windows cobertura): `ContextRequestFor` line 123 at 4/6 - the pattern `is not { UserName.Length: > 0 }` checks a null credential (no test passed one) and a null `UserName` (impossible: `NetworkCredential.UserName` never returns null). `Step` line 60 at 5/6 - SSPI never answers `ContinueNeeded`/`Completed` with a null token in the Windows tests; only the Linux test reached the other branches.
- `ContextRequestFor` now tests `string.IsNullOrEmpty(credential?.UserName)`, so the impossible null-`UserName` branch is gone; new test `ContextRequestFor_NoCredential_AsksForTheDefaultCredentials` covers the null credential.
- `Step`'s mapping moved into `internal static SystemSecurityContext.StepOf(code, token)`: the seam takes the BCL's answer as arguments rather than a factory for `NegotiateAuthentication`, since only the answer needs choosing. Behaviour is unchanged on both platforms; the existing Windows and Linux tests are untouched. Three `StepOf_*` tests cover a token, no token, and a failure carrying a token.
- `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` on Windows: 0 failing members. `Curl.Authentication.UnitTests`: 815 passed, 4 skipped (OS-conditioned), 0 failed; `dotnet build Curl.Authentication.UnitTests -warnaserror` clean.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl.Authentication measures 100% line and branch on Windows: ContextRequestFor and SystemSecurityContext.Step fully covered
