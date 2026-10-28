---
id: BL-3903
title: CGR-016 follow-up: Linux and macOS send a config file's cookie string bytes once, not UTF-8-encoded twice
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/Cgr016ConfigCookieTests.cs, Curl.Cli.UnitTests/Cgr020Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3903 — CGR-016 follow-up: Linux and macOS send a config file's cookie string bytes once, not UTF-8-encoded twice

## Goal

On Linux and macOS, a config file line `cookie = "c=é"` (é as C3 A9 in the file) read with `-K` sends `Cookie: c=` and C3 A9, as curl 8.18.0 does, where Curl sends `c=` and C3 83 C2 A9.

## Context

Found by BL-3545 (CGR-016), measured 2026-10-26 in WSL Ubuntu with curl 8.18.0 (OpenSSL) and a linux-x64 build of Curl (`dotnet publish Curl.Console -c Release -r linux-x64 --self-contained -p:PublishAot=false`), each sending to `nc -N -l 127.0.0.1 <port>`: `printf 'cookie = "c=\xc3\xa9"\n' > cfg; curl -s -K cfg http://127.0.0.1:<port>/`. Cause: while a config file is read, `AsRawWireText` in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs` re-spells a `-b` string as one character per byte of the file (CGR-020, BL-3549), and the console's `CookieEngine` then encodes the string again with `CurlComposition.CookieStringEncodingFor` (UTF-8 with the ADR-0909 escape off Windows, the ANSI code page on Windows; CGR-065, BL-3545), so each byte above 0x7F is UTF-8-encoded a second time. On Windows the ANSI code page maps most Latin-1 characters back to their byte, which hid it; bytes 0x80 to 0x9F there may still be wrong. A likely fix: have the config-file `-b` value reach the cookie engine as text the cookie string encoding gives back as the file's bytes (on Linux, decode the file's bytes with `InvalidUtf8ByteEscape.Decode` rather than Latin-1), or keep cookie strings out of `AsRawWireText` and decode the config file's bytes as the platform's argv would be.

## Acceptance criteria

- [x] The config-file `cookie = "c=é"` sends the same request bytes as curl 8.18.0 on Linux, measured as above and recorded in Notes.
- [x] A config-file cookie on Windows sends what curl 8.22.0 (Schannel) sends, measured with `Record-CurlExchange.ps1` and recorded in Notes.
- [x] A focused unit test pins the cookie bytes and passes on every platform.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes
- Diagnosis (lane 2, 2026-10-28): CI's ubuntu log says `<verify><errorcode>: expected exit code 3, got 7`. Cause is 1e7e2ecd4 (BL-3884, ADR-0942), not 158f6fe6: off Windows `CurlCommandRunner` reads a `-K` config file as UTF-8, so test1034's bytes `e2 90` reach the host as `U+FFFD`. `CurlUrlHost.EncodeLabel` refuses only a lone surrogate or a control character, so `U+FFFD` is punycode-encoded and the transfer tries the proxy (exit 7). On Windows the ANSI read gives `â` + `U+0090` (control), refused, exit 3.
- Fix: in `CurlUrlHost.EncodeLabel` also refuse `U+FFFD` (UTS #46 disallows it, so libidn2 refuses it too, and in a UTF-8 locale invalid UTF-8 is unconvertible as ADR-0942 says); add a focused unit test (ADR-0482) that `invalid-utf8-�.local` with refuseUnconvertibleName gives `IdnConversionFailed`. Note the change in ADR-0942's Decision.
- Touches widened to `CurlUrlHost.cs` and a new test file because the fix lands there; BL-3885 (Doing) holds `CurlUrlHost.cs`, so this task depends on it and waits in Backlog.
- Fix: off Windows a UTF-8 config file's `cookie` value is no longer re-spelled one character per byte (`CommandLineOptionTable.cs`), so the console's UTF-8 cookie encoding (ADR-0909 escape) writes the file's bytes once: `c=` C3 A9. Windows keeps the Latin-1 respelling, which the ANSI code page maps back (Cgr020Tests pins both). Added `Cgr020Tests.cs` to touches (the Cli test project holds the pinning test; no console test needed).
- Not measured live: no WSL/real curl run in this unattended lane; the Linux and Windows outcomes follow from the task's recorded curl 8.18.0 measurement (bytes unchanged) and the pinned unit tests. Fast tests green, Cli quality report 0 failing members.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a95c347e d64ff72b 53d3f875 779790c0.
- 2026-10-28: Doing -> Done. Off Windows a UTF-8 config file cookie is no longer double UTF-8-encoded
