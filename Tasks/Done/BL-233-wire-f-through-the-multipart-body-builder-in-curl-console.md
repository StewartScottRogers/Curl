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
completed: 2026-09-26
---
# BL-233 — Wire -F through the multipart body builder in Curl.Console

## Goal

`-F` and `--form-string` build a multipart body with BL-205's builder and send it through the HTTP handler.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `POST / HTTP/1.1\r\nHost: 127.0.0.1:18082\r\nAuthorization: Basic dTpw\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 949\r\nContent-Type: multipart/form-data; boundary=------------------------H5US3YN5uWKNbfycvXmtss\r\n\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="a"\r\n\r\nb\r\n--------------------------H5US3YN5uWKNbfycvXmtss\r\nContent-Disposition: form-data; name="f"; filename="srv.py"\r\nContent-Type: text/plain\r\n\r\n<the file's bytes>\r\n--------------------------H5US3YN5uWKNbfycvXmtss--\r\n`.

## Acceptance criteria

- [x] With an injected boundary source, `curl -u u:p -F a=b -F f=@file http://...` sends the measured bytes for the file's contents.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered (2026-09-26): `MultipartFormPartMapping` maps `CommandLineOptions.FormParts` (`FormPartSpecification`) onto BL-205's `MultipartFormPart`; `CurlCommandRunner.TransferAsync` builds one body per URL with `MultipartFormBodyBuilder` after the URL and `-r` checks, returns its exit 26 failure with no connection, otherwise passes the `StreamBody` through `TransferContextFactory.Create` into `HttpRequestOptionsMapping.FromCommandLine` (form body wins; the parser already refuses `-F` with `-d`), and disposes it when the transfer ends. The runner takes an optional `MultipartFormBodyBuilder` (tests inject a fixed boundary and an in-memory file system); by default it reads the real disk, encodes text with `CredentialEncoding.ForPlatform` (ADR-0027: form text uses the same platform encoding as credentials) and draws `MultipartBoundary.CreateRandom` boundaries.
- Measured (2026-09-26): curl 8.21.0 (`/mingw64/bin/curl`, Schannel) via `Record-CurlExchange.ps1 -Port 18233 -CurlArgs @('--no-progress-meter','-u','u:p','-F','a=b','-F','f=@<temp>\srv.py','http://127.0.0.1:18233/')`, `srv.py` = `print(1)` LF: `POST / HTTP/1.1`, `Host: 127.0.0.1:18233`, `Authorization: Basic dTpw`, `User-Agent: curl/8.21.0`, `Accept: */*`, `Content-Length: 313`, `Content-Type: multipart/form-data; boundary=------------------------26sLGDDRgOYwegSEyKubav`, then the `a`=`b` part and the `f` part with `filename="srv.py"` and `Content-Type: application/octet-stream`, exit 0. The Context's sample shows `text/plain` for `srv.py`; that was not what curl 8.21.0 sends (BL-205 found the same), so the test pins `application/octet-stream`. Pinned byte for byte in `CurlCommandRunnerFormTests`. The recorder's `-CurlArgs` must be passed as a PowerShell array, and a relative file path resolves against the recorder's directory, not the caller's.
- Defaults taken: the body is built after the malformed-URL and range checks (curl reports those without touching form files too); `;encoder=` is not carried (BL-274); `@-`/`<-` stay BL-275.
- Follow-up filed: BL-297 (a `-F` stream body is not rewound when `-L` re-sends it after a 307/308).
- Gates: `dotnet build -warnaserror` clean; `dotnet format --verify-no-changes` clean for both projects; `dotnet test --filter "TestCategory!=Integration"` green (Curl.Console.UnitTests 364 passed, 6 new); `Measure-CodeQuality.ps1 -Library Curl.Console` 100% line, 100% branch, 0 failing of 159 members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -F and --form-string build curl 8.21.0's multipart/form-data body and send it through the HTTP handler; an unopenable form file exits 26 before connecting
