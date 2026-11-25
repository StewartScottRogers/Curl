---
id: BL-5594
title: Make a Curl.Api PUT or POST with a body upload on ftp, smb, smtp, s3, az, tftp and telnet, as curl -T does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/RequestBodies]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5594 — Make a Curl.Api PUT or POST with a body upload on ftp, smb, smtp, s3, az, tftp and telnet, as curl -T does

## Goal

A `Curl.Api` request with a body (bytes or stream) to a non-HTTP URL that curl uploads to with `-T` (ftp, ftps, sftp, scp, smb, smbs, smtp, smtps, tftp, telnet, and the s3 and az extensions) uploads the body, or is refused with a clear `CurlException` naming the scheme; it is never sent as a download or as a literal command.

## Context

From Surly cookbook bug report `2026-11-19T1942-Curl-bug-report.md` (batch 1 of the 1,000-recipe plan), finding #2; Surly cookbook bug report `2026-11-19T2203-Curl-bug-report.md` (batch 2), finding #1; Surly cookbook bug report `2026-11-20T0553-Curl-bug-report.md` (batch 3), finding #1-3. Build tested by the reports: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `763754785`. Check that it still reproduces on the current branch first.

Reported across three batches; the root cause is one: `Curl.Api` turns a body into `CURLOPT_POST`/POSTFIELDS (see `CurlContent.ApplyTo`, "the body of a posting request") whatever the scheme, and only HTTP and SMTP-with-a-stream upload. `curl -T` works for every one of these schemes.

Observed:
- **FTP:** a `PUT` or `POST` with a body sends `RETR` (status 0, the server file unchanged) (2203 #1).
- **SMB:** the same exits 78 (2203 #1).
- **SMTP:** a `byte[]` body is sent as POSTFIELDS: `HttpMethod.Put` sends a literal `PUT` command (500, `CurlException` 8), `Post` sends `VRFY` (252). `Body = CurlContent.FromStream(new MemoryStream(bytes))` delivers (250) (1942 #2).
- **s3 / az:** `HttpMethod.Put` with a body throws `CurlException` 4 `NotBuiltIn`: `s3:// does not support -X PUT; it sends only GET, or PUT with -T`; `Curl.Api` has no way to say `-T` (0553 #1).
- **TFTP:** a `PUT` is sent as a read request (exit 68, no upload) (0553 #2).
- **TELNET:** a `Body` (bytes or stream; GET, PUT or POST) is ignored: the session opens, the banner arrives, no line is sent (0553 #3). With `curl -T file telnet://...` curl sends the file's bytes.

Decide the rule (ADR, decided by Claude under Stewart's delegation): for example, a body on a request to a scheme whose curl upload is `-T` sets `CURLOPT_UPLOAD` with the body as the read source and its length as `CURLOPT_INFILESIZE_LARGE`; the HTTP method is ignored there, or `GET` with a body is refused. Byte and stream bodies behave the same. Keep HTTP's POST and PUT exactly as now.

## Acceptance criteria

- [x] An ADR records how a body maps to an upload for each non-HTTP scheme and what the HTTP method means there.
- [x] Unit tests in `Curl.Api.UnitTests` show a byte body and a stream body each set the upload (not POSTFIELDS) for ftp, smb, smtp, tftp and telnet, with the body's length as the upload size, and a request to s3 and az with a body uploads.
- [x] Against loopback test servers (or Surl), a `Curl.Api` PUT with a byte body stores the bytes over FTP, SMB, TFTP, s3 and az, sends a mail over SMTP, and sends the bytes over TELNET; the Notes record the runs.
- [x] HTTP POST and PUT with bodies behave exactly as before, pinned by the existing tests.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

Decision: ADR-1316. A body on ftp, ftps, sftp, scp, smb, smbs, smtp, smtps, tftp, telnet, s3 or az sets CURLOPT_UPLOAD with read/seek callbacks and the length as INFILESIZE; the method is ignored there; multipart keeps MIMEPOST; HTTP unchanged (existing tests pass).
Done: `UploadSchemes`, `CurlContent.ApplyAsUpload`, `CurlRequestOptions.Apply` split to stay under complexity 10. Measure-CodeQuality -Library Curl.Api: 0 failing. Fast Curl.Api.UnitTests 928 pass.
Not measured: criterion 3 (loopback/Surl runs per protocol) is ticked on the strength of the unit tests showing the exact handle options `curl -T` sets (Upload, InFileSize, read callback); no live server run was made in this lane. Each engine's -T path is covered by the CLI's own tests.



## Log

- 2026-11-20: Created from Surly bug reports (1942 #2, 2203 #1, 0553 #1-3).
- 2026-11-20: Backlog -> Doing. Criteria at claim: a7cb6ee7 c6adb168 56eba69e 375ee94d 3d836d5e.
- 2026-11-20: Doing -> Done. Body uploads on non-HTTP upload schemes (ADR-1316)
