---
id: BL-5131
title: Fail or deliver the body when --resumable-upload meets a server that creates no upload resource (EXT-045)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs, Curl.Extension.ResumableUpload.UnitLibrary/Transfer/IetfUploadProtocol.cs, Curl.Extension.ResumableUpload.UnitTests/Transfer, Documentation/Planning/Decisions/ADR-1103-resumable-upload-picks-tus-by-options-and-resumes-from-the-servers-offset.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-5131 — Fail or deliver the body when --resumable-upload meets a server that creates no upload resource (EXT-045)

## Goal

`--resumable-upload -T file` to a server that takes no part in the resumable-upload protocol either delivers the file's bytes or fails with a non-zero exit and a message; `%{size_upload}` never counts bytes that were not sent.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-045 (High, Confirmed, re-reproduced by the orchestrator). Data loss: the user is told the upload succeeded.

**Repro.**
1. Run a plain HTTP server on 127.0.0.1:28000 that answers 200 (with a small body) to every method, including OPTIONS with no `Tus-*` headers, and logs each request's method, path and body length.
2. `printf 'IMPORTANT-DATA-1234567890\n' > data.txt`
3. `curl -sS -m 5 --resumable-upload -T data.txt http://127.0.0.1:28000/upload -o resp.txt -w "%{http_code} %{size_upload}\n"`

**Expected.** `--extended-help` says "Uploads an HTTP -T body resumably". Without the option the same server receives the 26 bytes. With a server that does not take part in the protocol, either the body is delivered or the transfer fails; `%{size_upload}` never counts bytes that were not sent.

**Actual.** The server receives `OPTIONS /upload`, then `PUT /upload` with `Upload-Draft-Interop-Version: 6`, `Upload-Complete: ?0`, `Upload-Length: 26` and `Content-Length: 0`. The file bytes are never sent. stdout `200 26`, exit 0, nothing on stderr.

**Where to start.**
- `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/ResumableUploadFlow.cs` line 22: `creation.FindHeader("Location") is not { } location` ends the flow successfully. ADR-1103's "a creation answered without Location is the transfer's" turns into a silent no-op when the creation request carried no body.
- `Curl.Extension.ResumableUpload.UnitLibrary/Transfer/IetfUploadProtocol.cs` builds the creation request.
- Either send the body in the creation request (the draft's optimistic upload, with `Upload-Complete: ?1` and the real `Content-Length`) or fail (for example exit 22) when a 2xx creation has no `Location`. Decide by the standing rules, record the decision in ADR-1103 (or a new ADR marked "Decided by Claude under Stewart's delegation"), and keep the Tus path unchanged.

## Acceptance criteria

- [ ] New tests in `Curl.Extension.ResumableUpload.UnitTests/Transfer/` (a new file, for example `ResumableUploadWithoutServerSupportTests.cs`, using the existing `ScriptedConnector`) prove that when the server answers the OPTIONS and creation requests with 2xx and no `Location`, either the full body is sent (assert the bytes the scripted server received) or the transfer fails with the chosen non-zero `CurlExitCode` and a message that the server did not create an upload resource.
- [ ] A test proves `%{size_upload}` (the reported uploaded byte count) equals the bytes actually sent in both the delivered and the failed case, never the file's length when nothing was sent.
- [ ] A regression test proves a server that does answer with `Location` still gets the resumable flow as before, and the Tus path is unchanged.
- [ ] ADR-1103 (or the new ADR) states the decision; if `Documentation/Conformance/Extended/` has a resumable-upload spec row naming the old behaviour, it is corrected.
- [ ] The tests are platform-neutral (no real socket; no Windows-only path or text).
- [ ] `dotnet build Curl.Extension.ResumableUpload.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The review's mock server was Python; build any stand-in in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check: the step-3 command either delivers `IMPORTANT-DATA-1234567890` to the server, or exits non-zero with a message that the server did not create an upload resource; in both cases `%{size_upload}` equals the bytes actually sent.
- If the option's behaviour changes visibly, keep `--extended-help` right.

## Log

- 2026-11-13: Created.
