---
id: BL-1132
title: Write every IMAP response line to the -D file as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1129]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1132 — Write every IMAP response line to the -D file as curl does

## Goal

An `imap://` or `imaps://` transfer with `-D` writes every response line it reads (tagged and untagged), byte for byte with its line ending and in arrival order, to the `-D` stream BL-1129 adds, and not the literal message data that goes to the output; under `-i` alone nothing extra is written, as curl 8.21.0 does.

## Context

- Measured on curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '* OK hi\r\nA001 BAD no\r\n'`: `curl -s -D <file> imap://127.0.0.1:<port>/` exits 56 and the file holds exactly `* OK hi\r\nA001 BAD no\r\n`; `curl -s -i` writes nothing to stdout.
- curl 8.21.0 `lib/pingpong.c` `Curl_pp_readresp` (https://github.com/curl/curl/blob/curl-8_21_0/lib/pingpong.c) passes every response line to `Curl_client_write(data, CLIENTWRITE_INFO, ...)`, which the tool writes to the `-D` stream. The literal after a `{size}` is body data and goes to the output instead.
- Before pinning a whole session, measure with `Record-CurlExchange.ps1 -Imap` (with `-D` and with `-i`): a `LIST` of the mailbox root (where the untagged `* LIST` lines are also the output), a `FETCH` by UID (the `* 1 FETCH (... {N}` line, the literal, and the line closing it) and a `SEARCH`. Record in Notes exactly which lines reach the `-D` file in each, since the listing lines are written both as INFO and as body.
- Code: `Curl.Protocol.Imap.UnitLibrary/ImapControlChannel.cs` (`ReadLineAsync(int lineBytes)`, literal reads separate); write to the BL-1129 property (read its Notes for the final name) when it is not `null`. `Curl.Protocol.Imap.UnitLibrary` may also have BL-1119 waiting; they share `touches` and run one after another.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Imap.UnitTests` pin the measured exchange above, and the measured `LIST`, `FETCH` and `SEARCH` sessions, so the new stream's bytes match real curl's `-D` file byte for byte and `Output` is unchanged.
- [ ] A test pins that with the new stream `null` and `HeaderOutput` set to `Output` (the `-i` shape) `Output` gains nothing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
