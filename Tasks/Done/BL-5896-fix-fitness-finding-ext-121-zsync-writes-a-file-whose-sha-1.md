---
id: BL-5896
title: Fix fitness finding EXT-121: --zsync writes a file whose SHA-1 differs from the control file and exits 0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.DeltaDownload.UnitLibrary/DeltaDownloadTransferHook.cs, Curl.Extension.DeltaDownload.UnitTests/DeltaDownloadTransferHookTests.cs, Documentation/Planning/Decisions/ADR-1092-zsync-fetches-only-the-blocks-a-seed-file-lacks-following-zsync-0-6.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5896 — Fix fitness finding EXT-121: --zsync writes a file whose SHA-1 differs from the control file and exits 0

## Goal

EXT-121 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: --zsync writes a file whose SHA-1 differs from the control file and exits 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (the documented integrity guarantee is bypassed) · **Confidence:** Confirmed (3 runs) · **Area:** zsync (`Curl.Extension.DeltaDownload`), Windows build.
- **Setup:** an HTTP server on 127.0.0.1:37200 that serves `f.bin` (10000 random bytes) with Range support; under `/tamper/f.bin` it flips byte 100 in every response, ranged or not. `f.zsync` has headers `zsync: 0.6.2`, `Filename: f.bin`, `Blocksize: 2048`, `Length: 10000`, `Hash-Lengths: 1,4,16`, `SHA-1: <SHA-1 of the real f.bin>`, followed by block checksums. `empty.seed` is an empty file.
- **Repro:** `curl -sS --zsync f.zsync --zsync-seed empty.seed http://127.0.0.1:37200/tamper/f.bin -o z2.bin -w "%{http_code}\n"; echo $?; sha1sum z2.bin`
- **Expected (ADR-1092):** on a SHA-1 mismatch the transfer runs again "so the output is never a wrong file"; the result must match the control file's SHA-1, and if it still does not, the transfer fails (for example exit 61, as `--checksum` does) without leaving a wrong file.
- **Actual (.NET):** prints `200`, exit 0; `z2.bin`'s SHA-1 (`88364691…`) differs from the control file's (`df606a73…`). The server log shows `Range: bytes=0-9999`, then a plain GET whose result is written without being checked.
- **Likely location:** `Curl.Extension.DeltaDownload.UnitLibrary/DeltaDownloadTransferHook.cs`, the fallback path that "runs the transfer again as curl runs it" (near the `SHA1.HashData` comparison).
- **Acceptance check:** the repro exits non-zero with a SHA-1 mismatch message and does not leave a wrong `z2.bin`; the untampered URL `http://127.0.0.1:37200/f.bin` still exits 0 with a byte-identical file.

## Acceptance criteria

- [x] the repro exits non-zero with a SHA-1 mismatch message and does not leave a wrong `z2.bin`; the untampered URL `http://127.0.0.1:37200/f.bin` still exits 0 with a byte-identical file.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced in the code: both fallback paths (the second run, and a 200 to a range) wrote unchecked.
  Decision in ADR-1347: every written body must match the control file's SHA-1, else exit 61 with nothing written.
- Repro run against a PowerShell loopback server: tampered URL exits 61, `--zsync: SHA-1 mismatch: ...`, no `z2.bin`;
  untampered URL exits 0 with a byte-identical file.
- Added to `touches`: the hook's test file and ADR-1092 (its fallback sentence became false); no other task in Doing
  named them.
- Measure-CodeQuality for the library: 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 2b100592 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. --zsync writes a body only when its SHA-1 matches the control file; otherwise exit 61 with nothing written (ADR-1347).
