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
completed: 2026-10-21
---
# BL-2918 — Exit 100 for an SFTP -Q path of 65535 bytes or more, as curl's sftp_quote returns Curl_get_pathname's CURLE_TOO_LARGE

## Goal

An SFTP `-Q` command whose path argument is 65535 bytes or more fails as curl 8.22.0 does (expected exit 100, `CURLE_TOO_LARGE`, since `sftp_quote` returns `Curl_get_pathname`'s code), instead of Curl's exit 21.

## Context

BL-2905 made `SftpQuoteCommand.ReadPathname` return `CurlExitCode.TooLarge` for such a path, but `ArgumentReader.Read` still turns every failure into null, so `ParseWithArguments` throws `SftpQuoteFailed` (exit 21). curl's `sftp_quote` (lib/vssh/libssh2.c, curl-8_22_0) reports `Syntax error: Bad first parameter` and returns the result itself. Measure real curl first (Record-CurlExchange.ps1 with a 70000-byte `-Q` path) before pinning the exit code and text.

## Acceptance criteria

- [x] A `-Q` command with a 70000-byte first path, and one with a 70000-byte second path, exit with the code and message measured from real curl, pinned in `SftpQuoteCommandTests`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Protocol.Ssh.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Measured 2026-10-21: Git for Windows' curl 8.22.0 (Schannel, libssh2 1.11.1) against a user-mode OpenSSH sshd in WSL on 127.0.0.1:2222 (RSA keys and group14/aes-ctr/hmac-sha2 added, since WinCNG libssh2 cannot negotiate sshd's defaults), each 70000-byte `-Q` command given through `-K` because a Windows command line stops at 32767 characters. `rm <70000 a>` exits 100 with `Syntax error: Bad first parameter to 'rm aaa…'` (cut to curl's error buffer, which `CurlErrorBuffer` in the console already does); `rename a <70000 a>` exits 100 with `Syntax error in rename: Bad second parameter`. A short `rm` of a missing file still exits 21.
- `ArgumentReader.Read` now hands back `Curl_get_pathname`'s code, and the bad first and second parameter failures throw `SshTransferException.SftpQuoteFailed(CurlExitCode, string)` with it, so a malformed argument stays exit 21 and a too-long one is exit 100. Pinned in `SftpQuoteCommandTests.Parse_PathOf70000Bytes_FailsWithExit100AndCurlsMessage`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line, 100% branch, max complexity 10, 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e4dee6c5 aa792584.
- 2026-10-21: Doing -> Done. An SFTP -Q path of 65535 bytes or more exits 100 with curl's bad first/second parameter text, as measured from curl 8.22.0
