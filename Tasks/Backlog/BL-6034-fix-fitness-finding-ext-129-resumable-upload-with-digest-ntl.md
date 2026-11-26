---
id: BL-6034
title: Fix fitness finding EXT-129: --resumable-upload with --digest, --ntlm, --negotiate or --anyauth sends the creation POST twice, leaving an orphan upload on the server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6034 — Fix fitness finding EXT-129: --resumable-upload with --digest, --ntlm, --negotiate or --anyauth sends the creation POST twice, leaving an orphan upload on the server

## Goal

EXT-129 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: --resumable-upload with --digest, --ntlm, --negotiate or --anyauth sends the creation POST twice, leaving an orphan upload on the server. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low (a stray empty upload is left; the data itself arrives correctly) · **Confidence:** Confirmed (mock: Digest and NTLM; real tusd: Digest) · **Area:** `--resumable-upload` with multi-pass authentication, Windows build. Next to last round's EXT-115 fix.
- **Server:** `docker run -d --name tusx -p 127.0.0.1:27341:8080 tusproject/tusd:v2.10.3 -host 0.0.0.0 -port 8080 -enable-experimental-protocol`
- **Repro:** `curl -sv --digest -u u:pw --resumable-upload -T up.txt http://127.0.0.1:27341/files -o /dev/null`, then count the uploads: `docker exec tusx sh -c 'ls /srv/tusd-data/data | grep -vc info'`.
- **Expected:** `--extended-help` (resumable-upload): one `-T` body uploaded by tus 1.0, i.e. one creation POST and one upload resource (plain `-u` gives a single POST).
- **Actual (.NET):** `> POST /files` (`Content-Length: 0`) → `201 Created`, then a second `> POST /files` → `201 Created`, then `PATCH` with `Content-Length: 0` and `PATCH` with `Content-Length: 3`; exit 0. tusd now holds two new uploads: the 3-byte one and a 0-byte orphan.
- **Likely location:** the resumable-upload hook (`Curl.Extension.ResumableUpload.UnitLibrary`, transfer code) now sends each request through curl's transfer; the multi-pass authentication probe (an empty request, then a repeat) is applied to the non-idempotent creation POST.
- **Acceptance check:** after the repro the upload count rises by exactly 1, and `-v` shows a single `> POST /files`.

## Acceptance criteria

- [ ] after the repro the upload count rises by exactly 1, and `-v` shows a single `> POST /files`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
