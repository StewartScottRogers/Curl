---
id: BL-3474
title: Run Extended curltest cases from one subfolder per protocol
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamConformanceTests.ExtendedCases.cs, Curl.Conformance.UnitTests/CurlTestCases/Extended/README.md, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj, Curl.Conformance.UnitTests/CLAUDE.md]
requirement: none
created: 2026-10-24
completed:
---
# BL-3474 — Run Extended curltest cases from one subfolder per protocol

## Goal

The Extended cases run from one subfolder per protocol under `Curl.Conformance.UnitTests/CurlTestCases/Extended/`, each case reported with its folder, and each protocol owning its own block of case numbers.

## Context

Stewart (2026-10-24) approved our own `.curltest` cases in one subfolder per protocol, beside `Original/` (ADR-0810, BL-3462). Today `UpstreamConformanceTests.ExtendedCases.cs` reads only `ext*.curltest` directly in `Extended/` (`ExtendedCaseNumbers`, `RunExtendedCasesAsync`). The 16 folders match Curl's protocol libraries; secure variants (HTTPS, FTPS, IMAPS, POP3S, SMTPS, LDAPS, SMBS, GOPHERS, WSS) go in their base protocol's folder, and SCP and SFTP in `SSH/`.

Number blocks, so parallel tasks never collide (decided by Claude under Stewart's delegation):
  - `DICT/`: `ext1001` to `ext1999`
  - `FILE/`: `ext2001` to `ext2999`
  - `FTP/`: `ext3001` to `ext3999`
  - `GOPHER/`: `ext4001` to `ext4999`
  - `HTTP/`: `ext5001` to `ext5999`
  - `IMAP/`: `ext6001` to `ext6999`
  - `LDAP/`: `ext7001` to `ext7999`
  - `MQTT/`: `ext8001` to `ext8999`
  - `POP3/`: `ext9001` to `ext9999`
  - `RTSP/`: `ext10001` to `ext10999`
  - `SMB/`: `ext11001` to `ext11999`
  - `SMTP/`: `ext12001` to `ext12999`
  - `SSH/`: `ext13001` to `ext13999`
  - `TELNET/`: `ext14001` to `ext14999`
  - `TFTP/`: `ext15001` to `ext15999`
  - `WS/`: `ext16001` to `ext16999`

## Acceptance criteria

- [x] `Extended/` holds the 16 subfolders above, each with a `.gitkeep` or its cases, and the csproj copies `Extended/**/ext*.curltest` to the test output directory.
- [x] `RunExtendedCasesAsync` runs every `ext<number>.curltest` in `Extended/` and its subfolders, and a failure names it as `<FOLDER>/ext<number>`.
- [x] A case whose number is outside its folder's block, or a number used twice, fails `ExtendedCases_RunThroughCurl_AllPass` with a message naming the file.
- [x] `RunExtendedCasesAsync_ThrowawayCaseInATemporaryFolder_IsFoundAndRun` (or a sibling test) covers a case in a subfolder, an out-of-block number and a duplicate.
- [x] `Extended/README.md` and `Curl.Conformance.UnitTests/CLAUDE.md` describe the folders and the number blocks.
- [x] `dotnet build` is clean and the fast tests of `Curl.Conformance.UnitTests` pass.

## Notes

- Root-level `ext<number>` files stay allowed (not block-checked), so the existing throwaway test keeps its shape. Block of a folder is its position times 1000 plus 1 to 999. Fast Extended tests (8) pass; dotnet build clean; CI on Linux and macOS is covered by platform-neutral paths (relative paths normalised to `/`).

## Log

- 2026-10-24: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b6539a77 a716d186 0bbd949c d625f67a e81fd22c 8939ec04.
