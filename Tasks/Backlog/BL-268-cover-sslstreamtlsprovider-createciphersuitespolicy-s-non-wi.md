---
id: BL-268
title: Cover SslStreamTlsProvider.CreateCipherSuitesPolicy's non-Windows line on Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-268 — Cover SslStreamTlsProvider.CreateCipherSuitesPolicy's non-Windows line on Windows

## Goal

`SslStreamTlsProvider.CreateCipherSuitesPolicy` in `Curl.Networking.UnitLibrary` is reached by tests on Windows in every branch, so the Networking quality gate is 100% line and branch on Windows, with no `ExcludeFromCodeCoverage` and no change in behaviour.

## Context

- Pre-existing since BL-066; found during BL-212. `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` on Windows reports `CreateCipherSuitesPolicy` at 92.31% line / 87.5% branch, because the last line, `return (new CipherSuitesPolicy(suites), null);` (`Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs`, around line 227), runs only off Windows: `CipherSuitesPolicy` throws `PlatformNotSupportedException` on Windows, and analyzer CA1416 requires the `OperatingSystem.IsWindows()` guard in front of it.
- ADR-0011 decides the cipher behaviour (Schannel build refuses `--ciphers` and ignores `--tls13-ciphers`; OpenSSL build applies them via `OpenSslCipherSuites`). That behaviour must not change on any platform.
- Suggested shape: an injected platform seam - e.g. an internal constructor parameter taking a `Func<IEnumerable<TlsCipherSuite>, CipherSuitesPolicy?>` policy factory (or a bool "platform supports CipherSuitesPolicy" plus a factory), whose production value keeps the `OperatingSystem.IsWindows()` check and CA1416-compliant construction in one small member, while tests supply a factory that records the suites. The public constructors keep their signatures. Whatever member ends up holding the platform check must itself be fully covered on Windows (for example by being an expression the tests call on both outcomes); if a single guarded `new CipherSuitesPolicy(...)` line remains unreachable on Windows, the task is not done.
- Tests go in `Curl.Networking.UnitTests` beside the existing `SslStreamTlsProvider` tests; `InternalsVisibleTo` is already how that project reaches internals (confirm in the csproj or `AssemblyInfo`).

## Acceptance criteria

- [ ] `SslStreamTlsProvider.cs` contains no `ExcludeFromCodeCoverage` attribute, and `CodeMetricsConfig.txt` is unchanged.
- [ ] A named test in `Curl.Networking.UnitTests` shows that, for the OpenSSL build with a valid `--ciphers` list, the suites `OpenSslCipherSuites.Select` returned reach the policy factory and the resulting policy is the one set on `SslClientAuthenticationOptions.CipherSuitesPolicy`; the existing cipher tests (Schannel refusal, unapplied message on Windows) still pass unchanged.
- [ ] On Windows, `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` reports 100% line and 100% branch coverage for `SslStreamTlsProvider` and every member this task adds.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean (CA1416 included); `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
