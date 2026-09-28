---
id: BL-373
title: Encode command-line header text in the platform curl's encoding in the HTTP formatter
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-252]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-373 — Encode command-line header text in the platform curl's encoding in the HTTP formatter

## Goal

`-H` and `--proxy-header` text reaches the wire in the platform curl's encoding: the system ANSI code page with best fit on Windows, UTF-8 on Linux and macOS, as ADR-0067 decides.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0067-command-line-header-text-becomes-bytes-in-the-platform-curls-encoding-inside-the-http-formatter.md` (from BL-252).
- `Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs` builds the head as a string and ends with `Encoding.Latin1.GetBytes`; custom header text must be encoded in the command-line encoding and written one character per byte, leaving the request target and the Bearer token as they are.
- Add an `Encoding` for command-line text to `HttpRequestOptions` (default: `Encoding.Latin1`, today's behaviour). `Curl.Console` sets it to `CredentialEncoding.ForPlatform(runsOnWindows)`, the encoding it already gives the authenticator (`Curl.Console/CurlCommandRunner.cs`).
- Measured Windows bytes (curl 8.21.0 mingw, Windows-1252): `é` -> `0xE9`, U+0100 -> `A`, `€` -> `0x80`, `中` -> `?`.
- `HttpRequestOptions` lives in `Curl.Protocol.Abstractions.UnitLibrary`, a shared contract, so this task runs apart from other protocol tasks.

## Acceptance criteria

- [ ] A `HttpRequestHeadFormatterTests` test with the Windows-1252 encoding set sends `X-A: é`, `X-B: Ā`, `X-C: €`, `X-D: 中` as the bytes `E9`, `41`, `80`, `3F`.
- [ ] A `HttpRequestHeadFormatterTests` test with UTF-8 set sends `X-A: é` as `C3 A9`, and `--proxy-header` text follows the same encoding.
- [ ] With no encoding set, the existing `Format_NonAsciiHeader_SendsLatin1WithBestFit` test passes unchanged.
- [ ] `Curl.Console` passes `CredentialEncoding.ForPlatform(runsOnWindows)` as the header encoding, pinned by a `Curl.Console.UnitTests` test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
