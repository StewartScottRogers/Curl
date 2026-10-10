---
id: BL-2022
title: Measure and close coverage of FTP kept control connections (BL-1981 follow-up)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2022 — Measure and close coverage of FTP kept control connections (BL-1981 follow-up)

## Goal

Run `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` after BL-1981 and cover every line and branch it added: FtpKeptConnection (ShutDownAsync with QuitsOnShutDown false, an IOException and a reply timeout), FtpControlChannel.ReadFirst/UnreadBytes, FtpUrlPath.DirectoryToRemember and IsReachedFrom, FtpSession.ChangeToPathDirectoryAsync and TryKeepControlConnection (a connection AUTH replaced is not kept).

## Context

BL-1981 (ADR-0467) added FTP control-connection reuse; its lane ran out of budget before measuring coverage. Tests: Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerKeptConnectionTests.cs.

## Acceptance criteria

- [ ] Measure-CodeQuality reports 100% line and branch coverage for Curl.Protocol.Ftp.UnitLibrary, complexity <= 10, CRAP <= 30.
- [ ] `dotnet build` clean and fast tests green.

## Notes

## Log

- 2026-10-10: Created.
