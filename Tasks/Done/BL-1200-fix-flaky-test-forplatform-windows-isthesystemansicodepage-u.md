---
id: BL-1200
title: Fix flaky test ForPlatform_Windows_IsTheSystemAnsiCodePage under the full parallel run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Authentication.UnitTests, Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1200 — Fix flaky test ForPlatform_Windows_IsTheSystemAnsiCodePage under the full parallel run

## Goal

`CredentialEncodingTests.ForPlatform_Windows_IsTheSystemAnsiCodePage` passes in every `dotnet test Curl.slnx` run, not only when run alone.

## Context

- Seen 2026-10-02 on dark factory lane 3 (BL-1164, Windows): the full fast run `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` failed it once (1 of 775 in `Curl.Authentication.UnitTests`); the same test run alone with `--filter Name~ForPlatform_Windows_IsTheSystemAnsiCodePage` passed. BL-1164 changed nothing in `Curl.Authentication.*`.
- The test compares `CredentialEncoding.ForPlatform(isWindows: true).CodePage` with `CodePagesEncodingProvider.Instance.GetEncoding(0)!.CodePage`. A likely cause is shared process state another test in the assembly changes in parallel (an encoding provider registration, or a cached encoding); start by finding which test touches `Encoding.RegisterProvider`, `CodePagesEncodingProvider` or `CredentialEncoding`'s cache.

## Acceptance criteria

- [x] The cause is named in Notes.
- [x] The test passes in 10 consecutive full `dotnet test Curl.Authentication.UnitTests` runs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- **Cause:** not shared .NET state. `CodePagesEncodingProvider.GetEncoding(0)` calls
  `Interop.Kernel32.TryGetACPCodePage`, which calls Windows' `GetCPInfoExW(CP_ACP)` on every
  call, uncached (read from the IL). When several threads make their first call in a process
  at once, Windows fails one of them with last error 0, and the provider answers `null` as
  if the host had no ANSI code page. MSTest runs this assembly at method level in parallel,
  so the test's own `GetEncoding(0)!` (NullReferenceException) or `ForPlatform`'s read
  (silent fallback to 1252, masked on a 1252 host) could lose the race.
- Measured with throwaway C# file-based apps outside the repo, 64 threads released at once:
  `GetEncoding(0)` null in 3-4 of 40 processes; `GetEncoding(1252)` never null (0 of 40);
  raw `GetCPInfoExW(CP_ACP)` failed in 6 of 40; an immediate retry succeeded 13 of 13.
- **Fix (decided by Claude):** `CredentialEncoding.ReadSystemAnsiCodePage(Func<Encoding?>)`
  reads code page 0 and reads again when the answer is `null` (`read() ?? read()`); on Linux
  and macOS both reads are `null`, so behaviour there is unchanged. The test's expected value
  retries the same way. Chosen over a P/Invoke of `GetACP`, which would need an OS branch
  that cannot be covered on one platform. Three new tests pin the retry.
- `SmtpCommandLineText` has the same single read; filed as BL-1201 (outside `touches`).
- `dotnet format --verify-no-changes` reports ENDOFLINE in `SystemSecurityContextFactoryTests.cs`
  and `NegotiateFailureLines.cs`, files this task did not change; the changed files are clean.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. ForPlatform and its test retry a null read of code page 0, which Windows fails for one of several concurrent first callers
