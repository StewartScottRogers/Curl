---
id: BL-1248
title: Fail a POP3 command whose send breaks with curl's exit 55
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1248 — Fail a POP3 command whose send breaks with curl's exit 55

## Goal

A POP3 transfer whose command cannot be written to the connection ends at once with exit 55 (`CurlExitCode.SendError`) and curl 8.21.0's message, instead of reading a reply that never comes.

## Context

- `Curl.Protocol.Pop3.UnitLibrary/Pop3ControlChannel.cs` `SendAsync` swallows the write's `IOException`, as SMTP's channel did before BL-1243.
- Follow the SMTP fix (BL-1243, ADR-0384): `Curl_pp_sendf` returns the send error, so curl ends with `CURLE_SEND_ERROR` (55): `Send failure: Connection was reset` for an `IOException` wrapping `SocketException` `ConnectionReset`, `Failed sending data to the peer` otherwise. Nothing more is sent or read, not even `QUIT`; `-v` writes the reset message but not the fallback text. Do not reference the SMTP library.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Pop3.UnitTests` with a fake connection whose write throws pin: a reset while sending `CAPA`, `USER` and `RETR` each give exit 55 `Send failure: Connection was reset`; any other `IOException` gives exit 55 `Failed sending data to the peer`; nothing further is sent or read after the failure.
- [ ] A cancelled token still throws `OperationCanceledException`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-1243.

## Log

- 2026-10-02: Created.
