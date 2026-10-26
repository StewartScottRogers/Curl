---
id: BL-3842
title: CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Gopher.UnitLibrary/GopherSelector.cs, Curl.Protocol.Gopher.UnitLibrary/GopherCommandLineText.cs, Curl.Protocol.Gopher.UnitTests/Cgr015Tests.cs, Curl.Protocol.Dict.UnitLibrary/DictRequest.cs, Curl.Protocol.Dict.UnitLibrary/DictCommandLineText.cs, Curl.Protocol.Dict.UnitTests/Cgr015Tests.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3Command.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3CommandLineText.cs, Curl.Protocol.Pop3.UnitTests/Cgr015Tests.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttTopic.cs, Curl.Protocol.Mqtt.UnitLibrary/MqttCommandLineText.cs, Curl.Protocol.Mqtt.UnitTests/Cgr015Tests.cs, Curl.Protocol.Gopher.UnitLibrary/Curl.Protocol.Gopher.UnitLibrary.csproj, Curl.Protocol.Dict.UnitLibrary/Curl.Protocol.Dict.UnitLibrary.csproj, Curl.Protocol.Mqtt.UnitLibrary/Curl.Protocol.Mqtt.UnitLibrary.csproj, Curl.Protocol.Dict.UnitLibrary/DictDiagnosticLog.cs, Curl.Protocol.Pop3.UnitLibrary/Pop3MessageId.cs, Curl.Protocol.Gopher.UnitTests/GopherProtocolHandlerTests.cs, Curl.Protocol.Mqtt.UnitTests/MqttProtocolHandlerTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3842 — CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows
# CGR-015 part 5: gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows

## Goal

Gopher selectors, dict words, POP3 custom requests (`-X`) and MQTT topics are sent as their bytes in the ANSI code page on Windows.

## Context

Split from BL-3544 (CGR-015 of `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`; read that task for every repro and the expected bytes). Decision (BL-3544, decided by Claude under Stewart's delegation): on Windows Curl matches the designated reference, the ANSI-argv Git for Windows ucrt64 curl 8.22.0 that `Record-CurlExchange.ps1` runs by default, so command-line text reaches the wire, files and standard output as its bytes in the system ANSI code page (cp1252 on the measuring machine); off Windows nothing changes (UTF-8). The existing hooks for that encoding are `Curl.Cli.UnitLibrary/ConfigFileWireText.WindowsAnsiCodePage` (`CommandLineOptions.ConfigFileWireTextEncoding`), `HttpRequestOptions.CommandLineTextEncoding` (set by the engine from `CredentialEncoding.ForPlatform`), and the per-library `SmtpCommandLineText` / `ImapCommandLineText` pattern. Tests stay platform-neutral: pass the encoding in (cp1252 from `CodePagesEncodingProvider`) rather than reading the host's. Measure real curl with `Record-CurlExchange.ps1` before pinning bytes.

Today `GopherSelector.cs`, `DictRequest.cs`, `Pop3Command.cs`/`Pop3MessageId.cs` and `MqttTopic.cs` encode with UTF-8. Follow `SmtpCommandLineText`: a per-library `<Protocol>CommandLineText` with `ForPlatform(isWindows, systemAnsiCodePage)`. MQTT payloads come from the `-d` bytes, which BL-3844 covers. Note the percent-decoded bytes of a URL stay as decoded; only literal non-ASCII characters change.

## Acceptance criteria

- [x] With cp1252: `gopher://127.0.0.1:<port>/0/é` sends selector `/` E9, `dict://127.0.0.1:<port>/d:é` sends `DEFINE ! ` E9, `-u u:p -X "XFOO é" pop3://...` sends `XFOO ` E9 and with `€` byte 80, and an MQTT topic `é` is E9, as real curl on Windows does (commands and both results in Notes).
- [x] A focused unit test in the changed library's own `.UnitTests` project (the `Cgr015` file named in `touches`) fails before the change and passes after it, with the encoding injected so it passes on Linux and macOS too.
- [x] Off Windows (UTF-8) the same commands send the same bytes as before.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for the changed libraries shows no member outside a gate.

## Notes

- Measured real curl 8.22.0 (Git for Windows ucrt64, code page 1252) on 2026-10-26 with `Record-CurlExchange.ps1`: `-s -S -m 5 gopher://127.0.0.1:P/0/é` sent `2F E9 0D 0A`; `-s -S -m 5 dict://127.0.0.1:P/d:é` sent `CLIENT libcurl 8.22.0` CRLF `DEFINE ! ` `E9` CRLF `QUIT` CRLF (the word escaped, as for any byte at or above 0x7F); `-s -S -u u:p -X "XFOO é" pop3://127.0.0.1:P/` (`-Pop3`) sent `XFOO E9` CRLF and with `€` `XFOO 80` CRLF; `-s -S -m 5 mqtt://127.0.0.1:P/é` (`-Script`: read, CONNACK, read) sent SUBSCRIBE `82 06 00 01 00 01 E9 00`. Curl now sends the same bytes with cp1252 injected (`Cgr015Tests` in each library); before the change those cases sent UTF-8 (`C3 A9`, and `E2 82 AC` for `€` on POP3), so the cp1252 tests fail against the old code.
- Decision (ADR-0820, decided by Claude under Stewart's delegation): a per-library `<Protocol>CommandLineText` like SMTP's; the static entry points keep their signatures and pass `Platform`, with an overload taking the encoding for tests. Literal characters become argv bytes before percent-decoding, so escapes still decode to the bytes they name. The POP3 message id read from the URL keeps UTF-8 (only `-X` was in scope).
- Off Windows: the UTF-8 tests pin the same bytes as before (`C3 A9`, dict `C3A9`, POP3 `XFOO Ã©`). CI on Linux and macOS: every new test injects its encoding, `Platform_ThisHost...` compares against the host's own choice, and the two host-dependent handler tests were made neutral, so the change is platform-neutral; fast tests green here.
- Touches added (no other Doing task on origin/work/dark-factory names Gopher, Dict, POP3 or MQTT): the Gopher, Dict and MQTT library csproj files for `InternalsVisibleTo` (the tests call the internal selector, request and topic builders with the encoding injected); `DictDiagnosticLog.cs` for a cref the new `TryEncode` overload made ambiguous; `Pop3MessageId.cs` for a `Decode(byte[])` overload, since `-X` is percent-decoded there; `GopherProtocolHandlerTests.cs` (its non-ASCII item-type test expected UTF-8 on every host) and `MqttProtocolHandlerTests.cs` (its 65535-byte topic tests built the topic from literal `€`, now written as `%E2%82%AC` so the byte count is the same on every host).
- Quality: `Measure-CodeQuality.ps1` for the four libraries: 100% line and branch coverage, 0 failing members.

## Log

- 2026-10-26: Created by BL-3544's run, which split CGR-015 by library.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 7cf24dc8 a9b9a7ee f4912012 326de9ca.
- 2026-10-26: Doing -> Done. Gopher selectors, dict words, POP3 -X commands and MQTT topics are sent in the ANSI code page on Windows (UTF-8 elsewhere), matching real curl
