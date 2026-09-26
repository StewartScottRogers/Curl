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
completed:
---
# BL-270 — Parse --ntlm and --negotiate into CommandLineOptions.AuthSchemes

## Goal

`--ntlm` and `--negotiate` parse into `CommandLineOptions.AuthSchemes` as curl 8.21.0's tool combines them.

## Context

- Found in BL-192, which parsed `--basic`, `--digest`, `--anyauth` and `--oauth2-bearer` (ADR-0026). Measured on curl 8.21.0 (mingw) on 2026-09-26: `--no-ntlm` and `--no-negotiate` are accepted (exit 7 against `http://127.0.0.1:1/`, not the cannot-be-reversed refusal).
- Add each as a `CommandLineOption.NegatableFlag` calling `CommandLineOptions.WantAuthScheme` with `HttpAuthSchemes.Ntlm` / `Negotiate`; measure first whether the Windows reference build refuses either as unsupported (as it refuses `--http2`, ADR-0017).

## Acceptance criteria

- [ ] `--ntlm`, `--negotiate`, `--no-ntlm` and `--no-negotiate` each have a test pinning the measured `AuthSchemes` or refusal.
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

## Log

- 2026-09-26: Created.
