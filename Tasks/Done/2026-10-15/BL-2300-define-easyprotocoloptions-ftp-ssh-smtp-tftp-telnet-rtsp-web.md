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
completed: 2026-10-15
---
# BL-2300 — Define EasyProtocolOptions (FTP, SSH, SMTP, TFTP, TELNET, RTSP, WebSocket) in Curl.Api.UnitLibrary with libcurl's defaults

## Goal

`Curl.Api.UnitLibrary/Options/Protocols` (namespace `Curl.Api.Options.Protocols`) defines `EasyProtocolOptions`, an immutable record with one nested record per protocol - FTP, SSH (SCP and SFTP), SMTP, TFTP, TELNET, RTSP, WebSocket - carrying every option of the plan's `EasyProtocolOptions` row, each defaulting to libcurl 8.22.0's default.

## Context

ADR-0506; plan section "CURLOPT groups". Follow the rules for option-group tasks written in BL-2294's Context. Nested records are `EasyFtpOptions`, `EasySshOptions`, `EasySmtpOptions`, `EasyTftpOptions`, `EasyTelnetOptions`, `EasyRtspOptions`, `EasyWebSocketOptions`. Enumerations: `FTP_FILEMETHOD` (`CURLFTPMETHOD_*`), `FTPSSLAUTH` (`CURLFTPAUTH_*`), `FTP_SSL_CCC` (`CURLFTPSSL_CCC_*`), `FTP_CREATE_MISSING_DIRS` (`CURLFTP_CREATE_DIR_*`), `SSH_AUTH_TYPES` (`CURLSSH_AUTH_*` flags), `RTSP_REQUEST` (`CURL_RTSPREQ_*`), `WS_OPTIONS` (`CURLWS_RAW_MODE`, `CURLWS_NOAUTOPONG`). IMAP, POP3, LDAP, DICT, GOPHER, MQTT and SMB take no options of their own beyond the shared groups; note that in a doc comment on `EasyProtocolOptions`. BL-2309 retargets the engine onto this record.

## Acceptance criteria

- [x] `EasyProtocolOptions` and its seven nested records exist in `Curl.Api.UnitLibrary/Options/Protocols` and carry every option in the plan's row, each documented with its `CURLOPT_*` name and default.
- [x] Tests in `Curl.Api.UnitTests/Options/Protocols` pin every default and enum value, and that a `with` copy changes only the property set.
- [x] This task's Notes list each `CURLOPT_*` of the plan's row with the property that carries it (none left without one).
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Property map (all on `EasyProtocolOptions`'s nested records):
  - FTP (`Ftp`): `FTPPORT` -> `ActiveAddress`; `QUOTE` -> `Quote`; `PREQUOTE` -> `PreQuote`; `POSTQUOTE` -> `PostQuote`; `APPEND` -> `Append`; `FTP_USE_EPRT` -> `UseEprt`; `FTP_USE_EPSV` -> `UseEpsv`; `FTP_USE_PRET` -> `UsePret`; `FTP_CREATE_MISSING_DIRS` -> `CreateMissingDirectories`; `FTP_ALTERNATIVE_TO_USER` -> `AlternativeToUser`; `FTP_SKIP_PASV_IP` -> `SkipPasvIp`; `FTPSSLAUTH` -> `SslAuthentication`; `FTP_SSL_CCC` -> `ClearCommandChannel`; `FTP_ACCOUNT` -> `Account`; `FTP_FILEMETHOD` -> `FileMethod`.
  - SSH (`Ssh`): `SSH_AUTH_TYPES` -> `AuthenticationTypes`; `SSH_COMPRESSION` -> `Compression`; `SSH_HOST_PUBLIC_KEY_MD5` -> `HostPublicKeyMd5`; `SSH_HOST_PUBLIC_KEY_SHA256` -> `HostPublicKeySha256`; `SSH_PUBLIC_KEYFILE` -> `PublicKeyFile`; `SSH_PRIVATE_KEYFILE` -> `PrivateKeyFile`; `SSH_KNOWNHOSTS` -> `KnownHostsFile`.
  - SMTP (`Smtp`): `MAIL_FROM` -> `MailFrom`; `MAIL_RCPT` -> `MailRecipients`; `MAIL_AUTH` -> `MailAuth`; `MAIL_RCPT_ALLOWFAILS` -> `MailRecipientAllowFails`.
  - TFTP (`Tftp`): `TFTP_BLKSIZE` -> `BlockSize`; `TFTP_NO_OPTIONS` -> `NoOptions`.
  - TELNET (`Telnet`): `TELNETOPTIONS` -> `TelnetOptions`.
  - RTSP (`Rtsp`): `RTSP_REQUEST` -> `Request`; `RTSP_SESSION_ID` -> `SessionId`; `RTSP_STREAM_URI` -> `StreamUri`; `RTSP_TRANSPORT` -> `Transport`; `RTSP_CLIENT_CSEQ` -> `ClientCSeq`; `RTSP_SERVER_CSEQ` -> `ServerCSeq`.
  - WebSocket (`WebSocket`): `WS_OPTIONS` -> `Options`.
- Enums: `FtpFileMethod`, `FtpSslAuthentication`, `FtpClearCommandChannel`, `FtpCreateMissingDirectories`, `SshAuthenticationTypes` (`[Flags] : uint`, `Any = ~0u` as `HttpAuthenticationMethods` does), `RtspRequest`, `WebSocketOptionBits` (`[Flags]`; named for the bits so it does not read as a second options record).
- Defaults taken from curl 8.22.0's option man pages: `FTP_USE_EPRT`, `FTP_USE_EPSV` and `FTP_SKIP_PASV_IP` default on; `FTP_FILEMETHOD` defaults to `CURLFTPMETHOD_MULTICWD` (what `CURLFTPMETHOD_DEFAULT` means); `SSH_AUTH_TYPES` to `CURLSSH_AUTH_ANY`; `TFTP_BLKSIZE` 512; `RTSP_REQUEST` `CURL_RTSPREQ_OPTIONS`; `RTSP_STREAM_URI` `*`; both CSeqs 0.
- Measure-CodeQuality.ps1 -Library Curl.Api.UnitLibrary: 0 failing members. Records only, so no complexity risk.
- Small shared `OptionRecordAssert.ChangesOnly` test helper in the test folder keeps the per-record `with` tests one line each.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyProtocolOptions carries every FTP, SSH, SMTP, TFTP, TELNET, RTSP and WebSocket CURLOPT_* at libcurl's default
