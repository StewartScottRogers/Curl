---
id: BL-2300
title: Define EasyProtocolOptions (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WebSocket) in Curl.Api.UnitLibrary with libcurl's defaults
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2292]
touches: [Curl.Api.UnitLibrary/Options/Protocols, Curl.Api.UnitTests/Options/Protocols]
requirement: none
created: 2026-10-15
completed:
---
# BL-2300 — Define EasyProtocolOptions (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WebSocket) in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Protocols` (namespace `Curl.Api.Options.Protocols`) defines `EasyProtocolOptions`, an immutable record with one nested record per protocol - FTP, SSH (SCP and SFTP), SMTP, TFTP, TELNET, RTSP, WebSocket - carrying every option of the plan's `EasyProtocolOptions` row, each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. Nested records are `EasyFtpOptions`, `EasySshOptions`, `EasySmtpOptions`, `EasyTftpOptions`, `EasyTelnetOptions`, `EasyRtspOptions`, `EasyWebSocketOptions`. Enumerations: `FTP_FILEMETHOD` (`CURLFTPMETHOD_*`), `FTPSSLAUTH` (`CURLFTPAUTH_*`), `FTP_SSL_CCC` (`CURLFTPSSL_CCC_*`), `FTP_CREATE_MISSING_DIRS` (`CURLFTP_CREATE_DIR_*`), `SSH_AUTH_TYPES` (`CURLSSH_AUTH_*` flags), `RTSP_REQUEST` (`CURL_RTSPREQ_*`), `WS_OPTIONS` (`CURLWS_RAW_MODE`, `CURLWS_NOAUTOPONG`). IMAP, POP3, LDAP, DICT, GOPHER, MQTT and SMB take no options of their own beyond the shared groups; note that in a doc comment on `EasyProtocolOptions`. BL-2309 retargets the engine onto this record.

## Acceptance criteria

- [ ] `EasyProtocolOptions` and its seven nested records exist in `Curl.Api.UnitLibrary/Options/Protocols` and carry every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [ ] Tests in `Curl.Api.UnitTests/Options/Protocols` pin every default and enum value, and that a `with` copy changes only the property set.
- [ ] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

## Log

- 2026-10-15: Created.
