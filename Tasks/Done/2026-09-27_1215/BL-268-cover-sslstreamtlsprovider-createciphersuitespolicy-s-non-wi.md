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
completed: 2026-09-27
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

- [x] `SslStreamTlsProvider.cs` contains no `ExcludeFromCodeCoverage` attribute, and `CodeMetricsConfig.txt` is unchanged.
- [x] A named test in `Curl.Networking.UnitTests` shows that, for the OpenSSL build with a valid `--ciphers` list, the suites `OpenSslCipherSuites.Select` returned reach the policy factory and the resulting policy is the one set on `SslClientAuthenticationOptions.CipherSuitesPolicy`; the existing cipher tests (Schannel refusal, unapplied message on Windows) still pass unchanged.
- [x] On Windows, `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` reports 100% line and 100% branch coverage for `SslStreamTlsProvider` and every member this task adds.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean (CA1416 included); `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`.

## Notes

- Shape chosen (test approach, delegated): `ICipherSuitesPolicyFactory` with production `CipherSuitesPolicyFactory(bool platformSupportsCipherSuitesPolicy)`; its `PlatformSupportsCipherSuitesPolicy` property carries `[UnsupportedOSPlatformGuard("windows")]`, so CA1416 accepts the guarded `new CipherSuitesPolicy(suites)` and tests reach both outcomes on Windows (`true` there throws `PlatformNotSupportedException`, which covers the line). `ForThisPlatform` = `new(!OperatingSystem.IsWindows())`, so behaviour is unchanged on every platform. No ADR: no curl-visible behaviour changed.
- A `Func<..., CipherSuitesPolicy?>` seam was tried first and rejected: CA1416 flags invoking a delegate whose type argument is the Windows-unsupported type. `Assert.AreSame<object>` in the test avoids the same flag on the generic assertion.
- To observe the options, `SslStreamTlsProvider` gained an internal `init` handshake step `AuthenticateSslStreamAsClientAsync`; the test records `SslClientAuthenticationOptions` there. Its default lives in a static field because a lambda in the property initializer added a delegate-cache branch to the constructor (complexity 12 > 10).
- Test policy on Windows is made with `RuntimeHelpers.GetUninitializedObject` (`Fakes/RecordingCipherSuitesPolicyFactory`), since the constructor throws there.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary -IncludeIntegration` -> 100% line, 100% branch, 0 failing members, worst CRAP 10. Networking fast tests 624 passed, 6 skipped. `dotnet format --verify-no-changes` still reports ENDOFLINE in `Curl.Output.UnitLibrary/X509CertificateFields.cs`; that file is outside this task's touches and was not changed.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. CipherSuitesPolicy construction is reached by tests on Windows through an injected factory; Networking is 100% line and branch on Windows
