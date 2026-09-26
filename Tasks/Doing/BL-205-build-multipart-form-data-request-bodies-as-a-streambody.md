---
id: BL-205
title: Build multipart/form-data request bodies as a StreamBody
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-159, BL-189]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-205 — Build multipart/form-data request bodies as a StreamBody

## Goal

A multipart builder in `Curl.Core.UnitLibrary` turns BL-189's part specifications into a `StreamBody` with a computed Content-Length, byte-equal to curl.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-u u:p -F a=b -F f=@srv.py`: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.
- Boundary: `------------------------` + 22 random alphanumerics in the header; each delimiter is `--` + boundary. Inject the boundary source.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] With an injected boundary the body is byte-equal to the measured one; Content-Length is known before sending.
- [ ] An unopenable file returns `CurlExitCode.ReadError` (26) with the measured message, at the moment curl reports it (measured).
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
