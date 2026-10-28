---
id: BL-3902
title: CGR-016 follow-up: Linux and macOS send a -X method holding an invalid UTF-8 byte as that byte
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Cli.UnitTests/Cgr016HeaderCookieTests.cs, Curl.Protocol.Http.UnitTests/Cgr016MethodTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3902 — CGR-016 follow-up: Linux and macOS send a -X method holding an invalid UTF-8 byte as that byte

## Goal

On Linux and macOS, `curl -s -X $'G\xffT' http://127.0.0.1:<port>/` sends the request line `47 FF 54 20 2F ...` (`G`, the byte FF, `T`), as curl 8.18.0 does, where Curl sends `47 C3 BF 54 ...`.

## Context

Found by BL-3545 (CGR-016), measured 2026-10-26 in WSL Ubuntu with curl 8.18.0 (OpenSSL) and a linux-x64 build of Curl (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`), each sending to `nc -N -l 127.0.0.1 <port>` (note: Ubuntu's Rust `timeout` refuses invalid UTF-8 arguments, so do not wrap the binaries in it). BL-3850 made `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` turn the escaped surrogate U+DCFF (ADR-0909, `InvalidUtf8ByteEscape`) into the character U+00FF and pinned only the parse (`Cgr016HeaderCookieTests.Parse_MethodOfInvalidByte_SendsTheByte`); something on the way to the request line (start at `HttpRequestHeadFormatter`, which encodes command-line text with `CommandLineTextEncoding` through `WireText`) then encodes U+00FF as UTF-8. BL-3545 fixed the same double encoding for `-b` by keeping the surrogate in the parse and letting the escape-aware encoding give the byte back; the likely fix here is the same: keep the surrogate for `-X` and let `WireText` (which already gives escaped surrogates back as their bytes) write the method. Check `-X` on Windows (argv never escaped there) and in a config file (CGR-020) still go out as before.

## Acceptance criteria

- [ ] `-X $'G\xffT'` sends the same request bytes as curl 8.18.0 on Linux, measured as above and recorded in Notes.
- [ ] Every `-X` value on Windows and every config-file `request` value goes out as before.
- [ ] A focused unit test pins the request line bytes and passes on every platform.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: af9dd051 b46d1a73 3645b810 779790c0.
