---
id: BL-5834
title: Fix fitness finding EXT-106: --content-digest checks nothing when combined with --segments
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs, Curl.Extension.SegmentedDownload.UnitLibrary/SegmentedDownloadTransferHook.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5834 — Fix fitness finding EXT-106: --content-digest checks nothing when combined with --segments

## Goal

EXT-106 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --content-digest checks nothing when combined with --segments. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Medium (silent loss of a requested integrity check; exit 0 with a mismatched digest)
- **Confidence:** Confirmed (Windows 3 runs, Linux 1 run)
- **Protocol/area:** `--content-digest` with `--segments`
- **Repro:** a server on 127.0.0.1:27020 whose `/cd_bigbad` serves 300,000 bytes with `ETag: "s"`, `Accept-Ranges: bytes`, a wrong `Repr-Digest: sha-256=:<base64 sha-256 of "other">:`, and answers Range requests correctly.
  1. `curl -sS --content-digest http://127.0.0.1:27020/cd_bigbad -o a.out` → `curl: (56) Repr-Digest sha-256 does not match the response body`, exit 56.
  2. `curl -sS --content-digest --segments 4 http://127.0.0.1:27020/cd_bigbad -o b.out`
- **Expected:** ADR-1087 says split transfers keep the single-transfer behaviour; `--extended-help` says content-digest "checks the server's digest fields against the response body". The joined body is the whole representation, so step 2 must exit 56 (or not split when `--content-digest` is set).
- **Actual (.NET):** step 2 exits 0 after 5 GETs and writes `b.out`. (`--checksum` with `--segments` correctly exits 61.)
- **Likely location:** `Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs` (add `--content-digest` to the do-not-split list) or `SegmentedDownloadTransferHook.cs` (check `Repr-Digest` on the joined body); the content-digest hook (ADR-1094).
- **Acceptance check:** step 2 exits 56 with the same message as step 1.

## Acceptance criteria

- [ ] step 2 exits 56 with the same message as step 1.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Doing. Criteria at claim: a38e0826 69116afe 3d836d5e.
