---
id: BL-3903
title: CGR-016 follow-up: Linux and macOS send a config file's cookie string bytes once, not UTF-8-encoded twice
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/Cgr016ConfigCookieTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3903 — CGR-016 follow-up: Linux and macOS send a config file's cookie string bytes once, not UTF-8-encoded twice

## Goal

On Linux and macOS, a config file line `cookie = "c=é"` (é as C3 A9 in the file) read with `-K` sends `Cookie: c=` and C3 A9, as curl 8.18.0 does, where Curl sends `c=` and C3 83 C2 A9.

## Context

Found by BL-3545 (CGR-016), measured 2026-10-26 in WSL Ubuntu with curl 8.18.0 (OpenSSL) and a linux-x64 build of Curl (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`), each sending to `nc -N -l 127.0.0.1 <port>`: `printf 'cookie = "c=\xc3\xa9"\n' > cfg; curl -s -K cfg http://127.0.0.1:<port>/`. Cause: while a config file is read, `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` re-spells a `-b` string as one character per byte of the file (CGR-020, BL-3549), and the console's `CookieEngine` then encodes the string again with `CurlComposition.CookieStringEncodingFor` (UTF-8 with the ADR-0909 escape off Windows, the ANSI code page on Windows; CGR-065, BL-3545), so each byte above 0x7F is UTF-8-encoded a second time. On Windows the ANSI code page maps most Latin-1 characters back to their byte, which hid it; bytes 0x80 to 0x9F there may still be wrong. A likely fix: have the config-file `-b` value reach the cookie engine as text the cookie string encoding gives back as the file's bytes (on Linux, decode the file's bytes with `InvalidUtf8ByteEscape.Decode` rather than Latin-1), or keep cookie strings out of `AsRawWireText` and decode the config file's bytes as the platform's argv would be.

## Acceptance criteria

- [ ] The config-file `cookie = "c=é"` sends the same request bytes as curl 8.18.0 on Linux, measured as above and recorded in Notes.
- [ ] A config-file cookie on Windows sends what curl 8.22.0 (Schannel) sends, measured with `Record-CurlExchange.ps1` and recorded in Notes.
- [ ] A focused unit test pins the cookie bytes and passes on every platform.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
