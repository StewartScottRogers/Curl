---
id: BL-5665
title: Upload an empty file over TFTP if curl 8.22.0 does, instead of exiting 26
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Tftp.UnitLibrary]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5665 — Upload an empty file over TFTP if curl 8.22.0 does, instead of exiting 26

## Goal

`curl -T empty.txt tftp://...` behaves as curl 8.22.0 does: an empty upload sends a WRQ and one empty DATA block, or fails with curl's own exit code and message.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 8 (check). Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

An empty file cannot be uploaded over TFTP: exit 26. RFC 1350 ends a transfer with a DATA block shorter than the block size, so an empty file is one DATA block of 0 bytes. Measure curl 8.22.0 against a TFTP server (Surl or `tftpd`), and match it; if curl uploads, the port's read of a zero-length source is wrongly treated as an unreadable file.

## Acceptance criteria

- [x] The Notes record curl 8.22.0's result for `-T empty.txt tftp://...`.
- [x] The port's result matches it, pinned by a test of the packets sent.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Does not reproduce on the current branch. Measured against a loopback PowerShell TFTP server: curl 8.22.0 (Schannel) `-T empty.txt tftp://127.0.0.1:6969/x` exits 0, sends a WRQ then one 4-byte DATA block 1 (0 payload bytes). Curl.Console does exactly the same, exit 0.
- Pinned already by `TftpUploadTests.ExecuteAsync_EmptyUpload_SendsTsizeZeroAndOneEmptyData1`. The exit 26 in the report was most likely already fixed or specific to the Surl server. No code change.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 8 (check).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 8beea2eb 6c434bcb 3d836d5e.
- 2026-11-20: Doing -> Done. Does not reproduce: Curl matches curl 8.22.0 (WRQ + one empty DATA, exit 0); already pinned by a test
