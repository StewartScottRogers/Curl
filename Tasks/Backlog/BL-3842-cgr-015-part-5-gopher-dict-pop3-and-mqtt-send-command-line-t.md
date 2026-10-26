---
id: BL-3842
title: CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary/GopherSelector.cs, Curl.Protocol.Gopher.UnitLibrary/GopherCommandLineText.cs, Curl.Protocol.Gopher.UnitTests/Cgr015Tests.cs, Curl.Protocol.Dict.UnitLibrary/DictRequest.cs, Curl.Protocol.Dict.UnitLibrary/DictCommandLineText.cs, Curl.Protocol.Dict.UnitTests/Cgr015Tests.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Command.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3CommandLineText.cs, Curl.Protocol.Pop3.UnitTests/Cgr015Tests.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttTopic.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttCommandLineText.cs, Curl.Protocol.Mqtt.UnitTests/Cgr015Tests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3842 — CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows
# CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows

## Goal

Gopher selectors, dict words, POP3 custom requests (`-X`) and MQTT topics are sent as their bytes in the ANSI code page on Windows.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Today `GopherSelector.cs`, `DictRequest.cs`, `Pop3Command.cs`/`Pop3MessageId.cs` and `MqttTopic.cs` encode with UTF-8. Follow `SmtpCommandLineText`: a per-library `<Protocol>CommandLineText` with `ForPlatform(isWindows, systemAnsiCodePage)`. MQTT payloads come from the `-d` bytes, which BL-3839 covers. Note the percent-decoded bytes of a URL stay as decoded; only literal non-ASCII characters change.

## Acceptance criteria

- [ ] With cp1252: `gopher://127.0.0.1:<port>/0/é` sends selector `/` E9, `dict://127.0.0.1:<port>/d:é` sends `DEFINE ! ` E9, `-u u:p -X "XFOO é" pop3://...` sends `XFOO ` E9 and with `€` byte 80, and an MQTT topic `é` is E9, as real curl on Windows does (commands and both results in Notes).
- [ ] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [ ] Off Windows (UTF-8) the same commands send the same bytes as before.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
