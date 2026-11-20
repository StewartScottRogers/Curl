---
id: BL-5610
title: Make a Curl.Api stream body to imap:// upload with its length, so APPEND works
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/RequestBodies]
requirement: none
created: 2026-11-20
completed:
---
# BL-5610 — Make a Curl.Api stream body to imap:// upload with its length, so APPEND works

## Goal

A `Curl.Api` `PUT` with a body (bytes, or a stream with or without a length) to an `imap://` mailbox appends the message, as `curl -T msg.eml imap://.../INBOX` does.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding widening BL-5594 (IMAP APPEND). Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

BL-5594 (Done) made a body upload over ftp, smb, smtp, s3, az, tftp and telnet; IMAP was not in it. `PUT` with `CurlContent.FromStream(stream, length)` to an IMAP mailbox fails `CurlException` 25, `Cannot APPEND with unknown input file size`: the stream body sets POSTFIELDSIZE, not INFILESIZE, so the IMAP handler never learns the length APPEND needs. Follow the rule BL-5594's ADR set for the other schemes (read it first) and add imap and imaps: a body sets `CURLOPT_UPLOAD` with its length as `CURLOPT_INFILESIZE_LARGE`; a stream with no length is refused with a clear message, since IMAP APPEND needs the size up front.

## Acceptance criteria

- [ ] Unit tests in `Curl.Api.UnitTests` show a byte body and a stream body with a length to an imap URL set the upload and the length (not POSTFIELDS).
- [ ] Against an IMAP test server (or Surl), a `Curl.Api` `PUT` of a message to `imap://.../INBOX` appends it, and a following fetch returns the same bytes; the Notes record the run.
- [ ] A stream body with no length to imap fails with a `CurlException` whose message says the size is needed.
- [ ] BL-5594's ADR lists imap and imaps; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding widening BL-5594 (IMAP APPEND).
