---
id: BL-6034
title: Fix fitness finding EXT-129: --resumable-upload with --digest, --ntlm, --negotiate or --anyauth sends the creation POST twice, leaving an orphan upload on the server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs, Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadSession.cs, Curl.Extension.ResumableUpload.UnitLibrary/CLAUDE.md, Curl.Extension.ResumableUpload.UnitTests/Transfer/ResumableUploadTransferHookTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] after the repro the upload count rises by exactly 1, and `-v` shows a single `> POST /files`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: the creation request carried an empty upload stream, so curl's handler treated it as a request with a body,
  sent it as the `--digest`/`--ntlm` probe, and after the 2xx sent it again. Fix (ADR-1428): creation has no body,
  only `Content-Length: 0`; a request without a body also drops `-H "Transfer-Encoding: ..."`, which would give it one.
- `touches` widened (no other task in Doing names these): `ResumableUploadSession.cs`, the library's `CLAUDE.md` and
  the hook tests.
- Pipeline run by the lane itself, without sub-agents: a two-file change.
- Repro run against a loopback tus mock that asks for no authentication (not real tusd, no Docker), with the built
  `curl.exe`: `--digest` and `--ntlm` gave `OPTIONS(0) POST(0) PATCH(0) PATCH(3)`, `--negotiate` and `--anyauth` gave
  `OPTIONS(0) POST(0) PATCH(3)`; each had one `> POST` line in `-v` and exit 0, so one upload resource is created.
- The empty probe `PATCH` is harmless under tus; under the IETF draft it carries `Upload-Complete: ?1`: BL-6044.
- Checks: build with warnings as errors clean, all fast tests green, then after a complexity split in `IsSent` the build
  again and the library's 116 tests; format check clean on the changed files; library at 100% line and branch coverage,
  no failing member.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 9280cbb9 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. --resumable-upload sends one creation request with --digest, --ntlm, --negotiate or --anyauth, so no orphan upload is left
