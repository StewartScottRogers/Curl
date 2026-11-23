---
id: BL-5832
title: Fix fitness finding EXT-104: --resumable-upload silently drops -u, --oauth2-bearer, -b, -A and -e, and sends no User-Agent
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5832 — Fix fitness finding EXT-104: --resumable-upload silently drops -u, --oauth2-bearer, -b, -A and -e, and sends no User-Agent

## Goal

EXT-104 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --resumable-upload silently drops -u, --oauth2-bearer, -b, -A and -e, and sends no User-Agent. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** Medium (an authenticated upload silently goes out with no credentials)
- **Confidence:** Confirmed (Windows 2 runs, Linux 1 run)
- **Protocol/area:** `--resumable-upload`
- **Repro:** `srv.py 27020 h1.log`, then `curl -sS --resumable-upload -T up.txt -u tu:TUSPASS http://127.0.0.1:27020/tus`. Repeat with `--oauth2-bearer T`, `-b c=v`, `-A X/1` and `-e http://r/`. Then `grep -iE "^(Authorization|Cookie|User-Agent|Referer)" h1.log`.
- **Expected:** ADR-1151 says the flag "takes over an HTTP -T upload". `-T -u tu:TUSPASS http://…` without the flag sends `Authorization: Basic dHU6VFVTUEFTUw==` and `User-Agent: curl/8.22.0`, so the OPTIONS, POST, HEAD and PATCH requests should carry the user's authentication, cookies, agent and referer.
- **Actual (.NET):** exit 0 and none of these fields appears on any request.
- **Likely location:** `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs` (only `Host`, the flow's own fields and `-H` lines are sent).
- **Acceptance check:** the `-u` command logs `Authorization: Basic dHU6VFVTUEFTUw==` on every request to 127.0.0.1:27020, and `-A X/1` logs `User-Agent: X/1`.

## Acceptance criteria

- [ ] the `-u` command logs `Authorization: Basic dHU6VFVTUEFTUw==` on every request to 127.0.0.1:27020, and `-A X/1` logs `User-Agent: X/1`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.