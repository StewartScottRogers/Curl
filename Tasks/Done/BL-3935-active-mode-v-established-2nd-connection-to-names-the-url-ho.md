---
id: BL-3935
title: Active-mode -v 'Established 2nd connection to' names the URL host ([::1]) as curl 8.22.0 does, not the server's data address
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary/FtpTransferMessages.cs, Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Protocol.Ftp.UnitTests/FtpActiveSecondConnectionLineTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3935 — Active-mode -v 'Established 2nd connection to' names the URL host ([::1]) as curl 8.22.0 does, not the server's data address

## Goal

In active mode, `-v` prints the accepted data connection's line as curl 8.22.0 does: `* Established 2nd connection to <URL host> (<server address> port <p>) from <local address> port <q> `, with an IPv6 URL host in brackets.

## Context

Found by BL-3695 (CGR-166). With `Record-CurlExchange.ps1 -Ftp -FtpData hello -ListenAddress ::1 -FtpReply 'EPRT=500 no'` and
`curl -v -P 127.0.0.1 --disable-eprt ftp://[::1]:<port>/file.txt`, curl 8.22.0 (Schannel, `C:\Program Files\Git\ucrt64\bin\curl.exe`) printed

`* Established 2nd connection to [::1] (127.0.0.1 port 55399) from 127.0.0.1 port 55398 `

and Curl printed

`* Established 2nd connection to 127.0.0.1 (127.0.0.1 port 61577) from 127.0.0.1 port 61576 `.

The host part is the URL host, not the server's data address. Curl builds the line in `FtpTransferMessages.SecondConnectionEstablished` from `remote.Address`, and the passive path (`FtpDataConnectEvents`) already names the URL host. The IPv6 bracket form of the first `Established connection to` line belongs to BL-3750 (CGR-221), not to this task.

## Acceptance criteria

- [x] The command above gives the same `Established 2nd connection to` line through Curl as through curl 8.22.0 (ports aside), and the IPv4 variant (`ftp://127.0.0.1:<port>/`) still matches.
- [x] A unit test in `Curl.Protocol.Ftp.UnitTests` pins the line for an IPv6 URL host with an IPv4 data address.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Compared with curl 8.22.0 per the task Context; the IPv6 URL host is bracketed, an IPv4 or name host is not. CI platform neutrality: the tests build lines from fixed endpoints only.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 121f4756 92074e78 779790c0.
- 2026-10-28: Doing -> Done. Active-mode -v 2nd connection line names the URL host, bracketed for IPv6; unit tests and quality run green
