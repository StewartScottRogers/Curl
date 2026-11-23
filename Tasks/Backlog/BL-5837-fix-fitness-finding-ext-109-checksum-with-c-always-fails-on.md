---
id: BL-5837
title: Fix fitness finding EXT-109: --checksum with -C - always fails on a correctly resumed download
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Checksum.UnitLibrary/ChecksumTransferHook.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5837 — Fix fitness finding EXT-109: --checksum with -C - always fails on a correctly resumed download

## Goal

EXT-109 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --checksum with -C - always fails on a correctly resumed download. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Low (wrong exit code; the file is correct)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** `--checksum`
- **Repro:** a server on 127.0.0.1:27020 whose `/strong` serves 300,000 bytes with `byte[i] = (i*13+5) % 256`, supporting ranges; `strong.ref` is the full body.
  1. `head -c 1000 strong.ref > c.out`
  2. `curl -sS -C - --checksum sha256:73cb3858a687a8494ca3323053016282f3dad39d42cf62ca4e79dda2aac7d9ac http://127.0.0.1:27020/strong -o c.out`
- **Expected:** `--extended-help`: "Verifies a download against an expected … digest". The digest is of the whole file and the resumed file is correct, so exit 0 (alternatively refuse `--checksum` with `-C`, exit 2, or document that only received bytes are hashed).
- **Actual (.NET):** `curl: (61) checksum mismatch: expected sha256:73cb…, got sha256:ff36…`, exit 61, while `cmp c.out strong.ref` reports the files equal.
- **Likely location:** `Curl.Extension.Checksum.UnitLibrary` transfer hook: seed the hash with the existing file prefix when the resume offset is above 0.
- **Acceptance check:** step 2 exits 0; the same command with a wrong digest still exits 61.

## Acceptance criteria

- [ ] step 2 exits 0; the same command with a wrong digest still exits 61.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.