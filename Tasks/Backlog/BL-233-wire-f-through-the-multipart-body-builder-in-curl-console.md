---
id: BL-233
title: Wire -F through the multipart body builder in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-205, BL-189, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-233 — Wire -F through the multipart body builder in Curl.Console

## Goal

`-F` and `--form-string` build a multipart body with BL-205's builder and send it through the HTTP handler.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.

## Acceptance criteria

- [ ] With an injected boundary source, `curl -u u:p -F a=b -F f=@file http://...` sends the measured bytes for the file's contents.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
