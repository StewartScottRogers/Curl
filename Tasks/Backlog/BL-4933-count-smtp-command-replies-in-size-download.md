---
id: BL-4933
title: Count SMTP command replies in %{size_download}
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Smtp.UnitLibrary/SmtpCommandTransfer.cs, Curl.Original.Protocol.Smtp.UnitTests/SmtpCommandSizeDownloadTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4933 — Count SMTP command replies in %{size_download}

## Goal

VRFY, EXPN, HELP and `-X` replies count toward `%{size_download}` as in curl.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-023 (Low, confirmed: reproduced at least twice).

- Repro: SMTP mock on 18500 answering `VRFY fred` with `250 Fred <fred@example.com>`. `curl -sS smtp://127.0.0.1:18500/ --mail-rcpt fred -w "%{size_download}\n"`
- Expected: REF 8.22.0: `250 Fred <fred@example.com>\r\n29\n`; HELP (`214-help line1\r\n214 end\r\n`) gives 25; `-X EXPN` gives 54.
- Actual: `...\r\n0\n`.
- Where to start: `SmtpCommandTransfer.SendCommandsAsync` and `SendRecipientCommandsAsync` return `TransferResult.Success(0)`, contradicting the class remarks.

## Acceptance criteria

- [ ] The repro prints `250 Fred <fred@example.com>\r\n29\n`.
- [ ] The HELP case ends with `25`.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
