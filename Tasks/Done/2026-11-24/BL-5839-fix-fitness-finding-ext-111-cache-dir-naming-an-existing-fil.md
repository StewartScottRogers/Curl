---
id: BL-5839
title: Fix fitness finding EXT-111: --cache-dir naming an existing file silently caches nothing, exit 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs]
requirement: none
created: 2026-11-23
completed: 2026-11-24
---
# BL-5839 — Fix fitness finding EXT-111: --cache-dir naming an existing file silently caches nothing, exit 0

## Goal

EXT-111 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --cache-dir naming an existing file silently caches nothing, exit 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** `--cache-dir` (neighbour of the fixed EXT-094 and EXT-095)
- **Repro:** `srv.py 27020 h.log`. `echo x > recfile`, then run `curl -sS --cache-dir recfile http://127.0.0.1:27020/plain` twice, then `grep -c ^GET h.log`.
- **Expected:** consistent with `--cache-dir "<>bad"` (`curl: --cache-dir cannot use '<>bad'` plus the try-help line, exit 2) and `--record recfile` (exit 23): an error or warning naming `--cache-dir`. With a real directory, the second request is served from cache (1 GET).
- **Actual (.NET):** exit 0, no message, 2 GETs.
- **Likely location:** `Curl.Extension.HttpCache.UnitLibrary/CacheDirectory.cs`.
- **Acceptance check:** the first command prints a message naming `--cache-dir` and `recfile` (exit 2 or 23, or a `Warning:` line).

## Acceptance criteria

- [x] the first command prints a message naming `--cache-dir` and `recfile` (exit 2 or 23, or a `Warning:` line).
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.
- 2026-11-23: Backlog -> Deferred. Stewart 2026-11-23: no more tasks this shift; finish Doing and stop. Move back to Backlog to resume.
- 2026-11-24: Deferred -> Backlog. Undeferred by Stewart 2026-11-24 to give the idle lanes work.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 35127fdc 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. CacheDirectory refuses a path naming an existing file with the --cache-dir cannot use message
