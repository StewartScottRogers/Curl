---
id: BL-5352
title: Restore the line breaks in TftpTransferTimingsTests' upload and download start-transfer tests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5352 — Restore the line breaks in TftpTransferTimingsTests' upload and download start-transfer tests

## Goal

`TftpTransferTimingsTests.cs` lays out its two BL-5327 tests as normally indented, multi-line methods that `dotnet format` leaves unchanged.

## Context

The shift-end review of 2026-11-17 (ADR-0710; pr-review-toolkit code-reviewer, confirmed by hand) found that
BL-5327's commit put `ExecuteAsync_Upload_HasNoStartTransferStampButANonZeroTotal` (and, after it,
`ExecuteAsync_Download_KeepsItsStartTransferStamp`) on a single physical line at
`Curl.Original.Protocol.Tftp.UnitTests/TftpTransferTimingsTests.cs:40`:
`[TestMethod]    public async Task ...()    {        var clock = ...`. The line breaks were lost.
The tests compile and pass. But the file breaks `.editorconfig` and the Arrange/Act/Assert blank-line layout, a
reader can't see the second test, and nothing in CI runs `dotnet format`, so nothing will catch it. This change is
formatting only; the test logic stays as it is.

## Acceptance criteria

- [ ] Each of the two tests begins on its own line with `[TestMethod]`, followed by an indented method body that
      has one statement per line and blank lines between Arrange, Act and Assert.
- [ ] `dotnet format Curl.Original.Protocol.Tftp.UnitTests --verify-no-changes` exits 0.
- [ ] Both tests still pass: `dotnet test Curl.Original.Protocol.Tftp.UnitTests --filter "FullyQualifiedName~StartTransfer"`.

## Notes

## Log

- 2026-11-17: Created.
