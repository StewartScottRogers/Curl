---
id: BL-270
title: Parse --ntlm and --negotiate into CommandLineOptions.AuthSchemes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-192]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-270 — Parse --ntlm and --negotiate into CommandLineOptions.AuthSchemes

## Goal

`--ntlm` and `--negotiate` parse into `CommandLineOptions.AuthSchemes` as curl 8.21.0's tool combines them.

## Context

- Found in BL-192, which parsed `--basic`, `--digest`, `--anyauth` and `--oauth2-bearer` (ADR-0026). Measured on curl 8.21.0 (mingw) on 2026-09-26: `--no-ntlm` and `--no-negotiate` are accepted (exit 7 against `http://127.0.0.1:1/`, not the cannot-be-reversed refusal).
- Add each as a `CommandLineOption.NegatableFlag` calling `CommandLineOptions.WantAuthScheme` with `HttpAuthSchemes.Ntlm` / `Negotiate`; measure first whether the Windows reference build refuses either as unsupported (as it refuses `--http2`, ADR-0017).

## Acceptance criteria

- [x] `--ntlm`, `--negotiate`, `--no-ntlm` and `--no-negotiate` each have a test pinning the measured `AuthSchemes` or refusal.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Measured curl 8.21.0 (x86_64-w64-mingw32, Schannel; Features include NTLM, SPNEGO, SSPI, Kerberos) on 2026-09-26: `--ntlm`, `--negotiate`, `--no-ntlm` and `--no-negotiate` are all accepted (exit 7 against `http://127.0.0.1:1/`), so neither is refused as unsupported, unlike `--http2` (ADR-0017).
- Against a local server answering a plain 200, with `-u u:p`: `--ntlm` sends `Authorization: NTLM TlRMTVNTUAAB...` at once (NTLM alone); `--negotiate` sends nothing (Negotiate alone waits for a challenge); `--ntlm --no-ntlm` and `--negotiate --no-negotiate` send `Basic dTpw` (empty set falls back to Basic); `--basic --ntlm` and `--ntlm --negotiate` send nothing (several schemes, curl waits for the 401). That is exactly a bit toggle, so both options are `NegatableFlag` rows calling `WantAuthScheme`, as the task proposed; no new design decision, so no new ADR (ADR-0026 covers the model).
- The full `/feature` agent pipeline was collapsed to a direct change: two table rows, nine `DataRow`s and doc-comment updates, all following BL-192's pattern. Gates run: `dotnet build` clean, fast tests green (Curl.Cli.UnitTests 1368 passed), `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --ntlm, --negotiate and their --no- spellings toggle Ntlm/Negotiate in CommandLineOptions.AuthSchemes as curl 8.21.0 does
