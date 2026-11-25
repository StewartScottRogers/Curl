---
id: BL-5865
title: Fix fitness finding EXT-115: --resumable-upload sends -u user:password as cleartext Authorization: Basic even with --aws-sigv4, --digest, --ntlm, --negotiate or --anyauth
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5865 — Fix fitness finding EXT-115: --resumable-upload sends -u user:password as cleartext Authorization: Basic even with --aws-sigv4, --digest, --ntlm, --negotiate or --anyauth

## Goal

EXT-115 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: --resumable-upload sends -u user:password as cleartext Authorization: Basic even with --aws-sigv4, --digest, --ntlm, --negotiate or --anyauth. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first.

Shared setup: **Shared mock for EXT-115, EXT-116, EXT-119, EXT-109, EXT-111** (HTTP, port 27020, logs every request line and header to a file): `OPTIONS /tus` → 204 with tus 1.0.0 headers offering `creation`; `POST /tus` → 201 with `Location: /files/1`; `PATCH /files/1` → 204; `/strong` → 300,000 bytes where `byte[i] = (i*13+5) % 256`, with ETag and Range support; `/plain` → a body with `Cache-Control: max-age=60`. `up.txt` is a 6-byte file. The finding, verbatim:


- **Severity:** High (credential leak: the password or AWS secret key is sent in clear) · **Confidence:** Confirmed (Windows twice, Linux once; re-verified by the orchestrator) · **Area:** `--resumable-upload`. Neighbour of this round's EXT-104 fix.
- **Repro:**
  - `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRETKEY99 --resumable-upload -T up.txt -o NUL http://127.0.0.1:27020/tus`, then check the server's `Authorization` headers.
  - Repeat with `--digest -u u:pw`, `--ntlm`, `--negotiate`, `--anyauth` instead of `--aws-sigv4 …`.
  - Control (no `--resumable-upload`): `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRETKEY99 -T up.txt -o NUL http://127.0.0.1:27020/x`
- **Expected (port's own behaviour and ADR-1151, "takes over an HTTP -T upload"):** the control sends `Authorization: AWS4-HMAC-SHA256 Credential=AKID/…` and never the secret; with `--digest` and no challenge the port sends no `Authorization` at all. The resumable upload must use the selected auth method, or refuse the combination with a clear error (e.g. exit 2 naming the option). It must never fall back to Basic.
- **Actual (.NET):** exit 0. OPTIONS, POST and PATCH all carry `Authorization: Basic QUtJRDpTRUNSRVRLRVk5OQ==` (`AKID:SECRETKEY99`) over plain HTTP. `--digest -u u:pw` sends `Authorization: Basic dTpwdw==` on all three. Same for every listed auth option, on Windows and Linux.
- **Likely location:** `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadRequestHeaders.cs`, `AuthorizationOf()`: returns `"Basic " + base64(user:password)` whenever `context.Credentials` has a user name, ignoring the selected auth method.
- **Acceptance check:** after the first repro, no `Authorization: Basic` line reaches the server and the requests carry `AWS4-HMAC-SHA256` (or the command exits non-zero naming the unsupported combination); with `--digest`, no Basic line appears.

## Acceptance criteria

- [x] after the first repro, no `Authorization: Basic` line reaches the server and the requests carry `AWS4-HMAC-SHA256` (or the command exits non-zero naming the unsupported combination); with `--digest`, no Basic line appears.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision: with -u and --aws-sigv4, --httpsig, --digest, --ntlm, --negotiate or --anyauth, --resumable-upload exits 2 (FailedInit) naming the option instead of sending Basic. It never sends the password in clear for another method. Only plain Basic sends Authorization: Basic. Measure-CodeQuality for the library: 100% line and branch, 0 failing members.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 7d7a5c0b 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. resumable upload refuses -u with another auth method instead of sending Basic
