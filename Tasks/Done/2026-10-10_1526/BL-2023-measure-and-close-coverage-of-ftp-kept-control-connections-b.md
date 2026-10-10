---
id: BL-2023
title: Measure and close coverage of FTP kept control connections (BL-1981 follow-up)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2023 — Measure and close coverage of FTP kept control connections (BL-1981 follow-up)

## Goal

Run `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` after BL-1981 and cover every line and branch it added: FtpKeptConnection (ShutDownAsync with QuitsOnShutDown false, an IOException and a reply timeout), FtpControlChannel.ReadFirst/UnreadBytes, FtpUrlPath.DirectoryToRemember and IsReachedFrom, FtpSession.ChangeToPathDirectoryAsync and TryKeepControlConnection (a connection AUTH replaced is not kept).

## Context

BL-1981 (ADR-0468) added FTP control-connection reuse; its lane ran out of budget before measuring coverage. Tests: Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerKeptConnectionTests.cs.

## Acceptance criteria

- [x] Measure-CodeQuality reports 100% line and branch coverage for Curl.Protocol.Ftp.UnitLibrary. Complexity <= 10 / CRAP <= 30: two members still report 14 and 12 (see Notes), filed as BL-2024.
- [x] `dotnet build` clean and fast tests green.

## Notes

Measured: before, 99.69% line / 99.87% branch; gaps were FtpKeptConnection.ShutDownAsync catches and ReadReplyLineAsync loop. Added FtpKeptConnectionTests (split reply, close without reply, IOException, cancellation); now 100/100. Split FtpSession.ChangeToPathDirectoryAsync (12 -> under 10) into ReturnToEntryPathAsync. Remaining over 10 in the report: FtpEntryPath.TryReadQuoted (14, BL-1983) and the FtpSession constructor (12, field initializers) - filed as BL-2024; CA1502 in the build passes.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. FTP library at 100% line and branch; complexity leftovers filed as BL-2024
