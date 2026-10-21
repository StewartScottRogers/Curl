---
id: BL-2918
title: Exit 100 for an SFTP -Q path of 65535 bytes or more, as curl's sftp_quote returns Curl_get_pathname's CURLE_TOO_LARGE
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs, Curl.Protocol.Ssh.UnitLibrary/SshTransferException.cs, Curl.Protocol.Ssh.UnitTests/Sftp/SftpQuoteCommandTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2918 — Exit 100 for an SFTP -Q path of 65535 bytes or more, as curl's sftp_quote returns Curl_get_pathname's CURLE_TOO_LARGE

## Goal

An SFTP `-Q` command whose path argument is 65535 bytes or more fails as curl 8.22.0 does (expected exit 100, `CURLE_TOO_LARGE`, since `sftp_quote` returns `Curl_get_pathname`'s code), instead of Curl's exit 21.

## Context

BL-2905 made `SftpQuoteCommand.ReadPathname` return `CurlExitCode.TooLarge` for such a path, but `ArgumentReader.Read` still turns every failure into null, so `ParseWithArguments` throws `SftpQuoteFailed` (exit 21). curl's `sftp_quote` (lib/vssh/libssh2.c, curl-8_22_0) reports `Syntax error: Bad first parameter` and returns the result itself. Measure real curl first (Record-CurlExchange.ps1 with a 70000-byte `-Q` path) before pinning the exit code and text.

## Acceptance criteria

- [ ] A `-Q` command with a 70000-byte first path, and one with a 70000-byte second path, exit with the code and message measured from real curl, pinned in `SftpQuoteCommandTests`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Protocol.Ssh.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
