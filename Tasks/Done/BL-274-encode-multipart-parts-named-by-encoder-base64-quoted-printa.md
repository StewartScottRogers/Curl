---
id: BL-274
title: Encode multipart parts named by ;encoder= (base64, quoted-printable, 7bit, 8bit, binary)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-205]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-274 — Encode multipart parts named by ;encoder= (base64, quoted-printable, 7bit, 8bit, binary)

## Goal

`MultipartFormBodyBuilder` sends a part whose `-F` spec carries `;encoder=` encoded as curl 8.21.0 encodes it, with the `Content-Transfer-Encoding` header and a correct `Content-Length`.

## Context

- Found while delivering BL-205 (2026-09-26): BL-189 parses `;encoder=` into `FormPartSpecification.Encoder`, but `MultipartFormPart` (ADR-0027) has no encoder yet.
- libcurl 8.21.0 `lib/mime.c` (`encoders[]`, `encoder_base64_*`, `encoder_qp_*`, `Curl_mime_prepare_headers`) is the reference; an unknown encoder name is refused by curl, so measure what it prints and exits with.
- Where a criterion says *measured*, run curl 8.21.0 (`/mingw64/bin/curl`) with `Record-CurlExchange.ps1`, record the command and bytes in `Notes`, then pin them in a test.

## Acceptance criteria

- [x] `MultipartFormPart` carries the encoder, and `base64`, `quoted-printable`, `7bit`, `8bit` and `binary` produce the body bytes and `Content-Length` measured on curl 8.21.0 for a text part and a file part.
- [x] An unknown encoder name gives the measured exit code and message.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- touches: added `Documentation/Planning/Decisions` for ADR-0040 and its README row; no task in Doing names it.
- Measured 2026-09-26 with `Record-CurlExchange.ps1 -CurlArgs -s,-S,-F,<spec>,http://127.0.0.1:<port>/` against `/mingw64/bin/curl` 8.21.0 (Schannel, code page 1252), `data.txt` = the UTF-8 `DataText` in `MultipartFormBodyBuilderEncoderTests`:
  - `t=hello = world é;encoder=base64` -> `Content-Transfer-Encoding: base64`, body `aGVsbG8gPSB3b3JsZCDp`, Content-Length 203; `8bit` 196, `binary` 198 (bytes as they are); `t=hi there;encoder=7bit` 189.
  - `f=@data.txt;encoder=base64` -> four 76-column CRLF lines, Content-Length 500; `8bit` 424, `binary` 426.
  - `quoted-printable` (text and file) -> `Transfer-Encoding: chunked`; empty data keeps Content-Length (193). Body bytes pinned in the tests.
  - `7bit` with a byte above 127 (text or file) -> exit 26, `curl: (26) read error getting mime data`.
  - `encoder=bogus` and `encoder=` -> exit 43, `curl: (43) A libcurl function was given a bad argument`; `encoder=BASE64` sends `base64`; own `Content-Transfer-Encoding` header replaces the generated one, body still encoded (182).
  - `-F a=@missing -F "b=x;encoder=bogus"` exits 26; the reverse order exits 43: parts fail in order.
- Decision (ADR-0040, decided by Claude under Stewart's delegation): transforming encoders read a file whole while building; `binary`/`8bit` still stream; a `7bit` refusal is reported after every other part is built.
- Default taken: `MultipartFormPart.Encoder` is an `init` property rather than a new positional parameter, so existing constructions are untouched.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` -> 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Follow-ups filed: BL-298 (map `FormPartSpecification.Encoder` in `Curl.Console`), BL-299 (stream the transforming encoders).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. MultipartFormBodyBuilder encodes ;encoder= parts (base64, quoted-printable, 7bit, 8bit, binary) byte for byte as curl 8.21.0, unknown encoder exit 43
