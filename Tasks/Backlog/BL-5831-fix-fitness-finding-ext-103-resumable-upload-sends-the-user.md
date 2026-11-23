---
id: BL-5831
title: Fix fitness finding EXT-103: --resumable-upload sends the user's -H headers, including Authorization and Cookie, to a cross-origin upload Location, even from https to http
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs, Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs]
requirement: none
created: 2026-11-23
completed:
---
# BL-5831 — Fix fitness finding EXT-103: --resumable-upload sends the user's -H headers, including Authorization and Cookie, to a cross-origin upload Location, even from https to http

## Goal

EXT-103 from the outside fitness review of 2026-11-23 09:23 no longer reproduces: --resumable-upload sends the user's -H headers, including Authorization and Cookie, to a cross-origin upload Location, even from https to http. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-23T0923` (commit `bef3629`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the OpenLDAP server from STD-117"), find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: Common mock used by EXT-103 to EXT-111: a Python HTTP server `srv.py PORT LOGFILE [tls]` that logs every request head (request line and all header fields) to LOGFILE; in `tls` mode it uses a certificate for 127.0.0.1/localhost signed by a test CA `ca.pem`. Paths: `POST /tus?loc=<url-encoded URL>` answers `201 Created` with `Location: <that URL>` and `Tus-Resumable: 1.0.0`; `PATCH /files/1` answers 204; `/go?to=<URL>` answers 302 to that URL; `/plain` answers 200 with `Cache-Control: max-age=60`. The finding, verbatim:

- **Severity:** High (credential sent to a party the server chose; in clear after a TLS downgrade)
- **Confidence:** Confirmed (reviewer 3 runs Windows, 1 run Linux; orchestrator re-reproduced on Windows)
- **Protocol/area:** `--resumable-upload` (tus 1.0), Windows and Linux builds
- **Repro:**
  1. Start `srv.py 27020 h1.log`, `srv.py 27022 h2.log` and `srv.py 27010 tls.log tls`. `up.txt` holds a few bytes.
  2. `curl -sS --resumable-upload -T up.txt -H "Authorization: Bearer TUSHDR" "http://127.0.0.1:27020/tus?loc=http%3A%2F%2Flocalhost%3A27022%2Ffiles%2F1"`
  3. `curl -sS --cacert ca.pem --ssl-no-revoke --resumable-upload -T up.txt -H "Authorization: Bearer TLSDOWNGRADE" "https://127.0.0.1:27010/tus?loc=http%3A%2F%2Flocalhost%3A27022%2Ffiles%2F1"`
  4. Control: `curl -sS -H "Authorization: Bearer REDIR" -L "http://127.0.0.1:27020/go?to=http://localhost:27022/plain"`
- **Expected:** the port drops `-H Authorization` and `-H Cookie` on a cross-origin `-L` (step 4 sends none to `localhost:27022`). ADR-1103 says the upload resource is the creation response's `Location` resolved against the URL; a `Location` on another origin, or an https→http change, must get the same treatment: no `Authorization`, `Cookie` or `Proxy-Authorization` from `-H` (or refuse the cross-origin upload with an error).
- **Actual (.NET):** exit 0. `h2.log` shows `PATCH /files/1` with `Authorization: Bearer TUSHDR`; step 3 sends `Authorization: Bearer TLSDOWNGRADE` over cleartext HTTP. `-H "Cookie: …"`, `-H "Proxy-Authorization: …"` and `-H "X-Api-Key: …"` are forwarded the same way. Step 4 sends no `Authorization`.
- **Likely location:** `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs` `For()` adds every `-H` line for any URI; the cross-origin `Location` comes from `Transfer/ResumableUploadFlow.cs` (~line 27).
- **Acceptance check:** after step 2, `grep -ci "^Authorization" h2.log` prints 0 (or the command exits non-zero naming the cross-origin upload URL). With `?loc=/files/1` (same origin) the header is still sent on PATCH.

## Acceptance criteria

- [ ] after step 2, `grep -ci "^Authorization" h2.log` prints 0 (or the command exits non-zero naming the cross-origin upload URL). With `?loc=/files/1` (same origin) the header is still sent on PATCH.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-23: Created from fitness review 2026-11-23T0923.