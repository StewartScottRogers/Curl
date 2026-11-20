---
id: BL-5616
title: Make Curl.Api CurlTftpOptions.BlockSize accept the same range libcurl's CURLOPT_TFTP_BLKSIZE does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlTftpOptions.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5616 — Make Curl.Api CurlTftpOptions.BlockSize accept the same range libcurl's CURLOPT_TFTP_BLKSIZE does

## Goal

A TFTP block size given to `Curl.Api` is accepted, clamped or refused exactly as libcurl's `CURLOPT_TFTP_BLKSIZE` and `curl --tftp-blksize` treat the same value.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 5. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`CurlTftpOptions.BlockSize` throws `ArgumentOutOfRangeException` outside 8 to 65464, while `curl --tftp-blksize 70000` clamps to 65464. The two should agree. Measure curl 8.22.0 (`--tftp-blksize 70000`, `4`, `0`) and read libcurl 8.22.0's `CURLOPT_TFTP_BLKSIZE` handling (it may return `CURLE_BAD_FUNCTION_ARGUMENT` while the tool clamps, or clamp itself); make `Curl.Api` follow libcurl, since it is the library surface, and document the range on the property.

## Acceptance criteria

- [x] The Notes record what curl 8.22.0 and libcurl 8.22.0 do with block sizes 0, 4, 65464 and 70000.
- [x] `CurlTftpOptions.BlockSize` behaves as libcurl does for each of those values, pinned by tests; its doc comment states the range and what happens outside it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced on the branch: BlockSize threw outside 8..65464. Libcurl's own setter (Libcurl/Options/Protocols/EasyProtocolOptionSetters.cs, ported from setopt.c 8.22.0) treats it: negative = CURLE_BAD_FUNCTION_ARGUMENT; 0 to 7 = not stored, 512 default; 8 to 65464 as given; above 65464 clamped to 65464. So 0, 4 -> 512; 65464 -> 65464; 70000 -> 65464. The tool's `--tftp-blksize 70000` clamps to 65464 likewise. I followed that port rather than re-measuring real curl.
- Curl.Api's BlockSize now does the same, only a negative value throws; doc comment states it; pinned by TftpBlockSize_FollowsLibcurl_FallsBackBelowEightAndClampsAbove. Measure-CodeQuality for Curl.Api: 0 failing members.

- Reproduced on the branch: BlockSize threw outside 8..65464. Libcurl's own setter (Libcurl/Options/Protocols/EasyProtocolOptionSetters.cs, ported from setopt.c 8.22.0) treats it: negative = CURLE_BAD_FUNCTION_ARGUMENT; 0 to 7 = not stored, 512 default; 8 to 65464 as given; above 65464 clamped to 65464. So 0, 4 -> 512; 65464 -> 65464; 70000 -> 65464. The tool's `--tftp-blksize 70000` clamps to 65464 likewise. I followed that port rather than re-measuring real curl.
- Curl.Api's BlockSize now does the same, only a negative value throws; doc comment states it; pinned by TftpBlockSize_FollowsLibcurl_FallsBackBelowEightAndClampsAbove. Measure-CodeQuality for Curl.Api: 0 failing members.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 5.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 072938f4 89ff7258 3d836d5e.
- 2026-11-20: Doing -> Done. BlockSize now follows libcurl: negative throws, 0-7 falls back to 512, above 65464 clamps
