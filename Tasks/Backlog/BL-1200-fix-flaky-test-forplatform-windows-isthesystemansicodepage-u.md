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
completed:
---
# BL-1200 — Fix flaky test ForPlatform_Windows_IsTheSystemAnsiCodePage under the full parallel run

## Goal

`CredentialEncodingTests.ForPlatform_Windows_IsTheSystemAnsiCodePage` passes in every `dotnet test Curl.slnx` run, not only when run alone.

## Context

- Seen 2026-10-02 on dark factory lane 3 (BL-1164, Windows): the full fast run `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` failed it once (1 of 775 in `Curl.Authentication.UnitTests`); the same test run alone with `--filter Name~ForPlatform_Windows_IsTheSystemAnsiCodePage` passed. BL-1164 changed nothing in `Curl.Authentication.*`.
- The test compares `CredentialEncoding.ForPlatform(isWindows: true).CodePage` with `CodePagesEncodingProvider.Instance.GetEncoding(0)!.CodePage`. A likely cause is shared process state another test in the assembly changes in parallel (an encoding provider registration, or a cached encoding); start by finding which test touches `Encoding.RegisterProvider`, `CodePagesEncodingProvider` or `CredentialEncoding`'s cache.

## Acceptance criteria

- [ ] The cause is named in Notes.
- [ ] The test passes in 10 consecutive full `dotnet test Curl.Authentication.UnitTests` runs.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
