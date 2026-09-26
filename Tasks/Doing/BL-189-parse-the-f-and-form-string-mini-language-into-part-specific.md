---
id: BL-189
title: Parse the -F and --form-string mini-language into part specifications
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-189 — Parse the -F and --form-string mini-language into part specifications

## Goal

`-F`/`--form` and `--form-string` parse into ordered part specifications (name, value or file, type, filename, headers).

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured `-F a=b -F f=@srv.py` body: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] `name=value`, `name=@file`, `name=<file`, `;type=`, `;filename=`, `;headers=` and quoting parse as measured on curl 8.21.0.
- [ ] A malformed spec gives the measured refusal and exit.
- [ ] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [ ] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
