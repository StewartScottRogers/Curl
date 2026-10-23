---
id: BL-3210
title: Record upstream libcurl 8.22.0 request bytes and codes for four MIME edge cases through a P/Invoke harness
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-LibcurlMimeExchange.cs, Documentation/Product/LibcurlMimeEdgeCaseRecordings.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3210 — Record upstream libcurl 8.22.0 request bytes and codes for four MIME edge cases through a P/Invoke harness

## Goal

The request bytes and final codes upstream libcurl 8.22.0 gives for BL-3208's four MIME edge cases are recorded, so BL-3211 to BL-3214 pin measured values.

## Context

Split from BL-3208 (Stewart's Libcurl API conformance batch, report 30). The curl tool cannot measure these: `-F a=@nope.txt` and `-F "a=<file;encoder=7bit"` over non-ASCII both exit 26 before connecting (the tool's own checks, measured 2026-10-22 with curl 8.22.0 from `/ucrt64/bin`), and the tool applies no encoder to a `=(` multipart part. They need libcurl called directly. `/ucrt64/bin/libcurl-4.dll` is libcurl 8.22.0 (Schannel): write a C# file-based app (`dotnet run Record-LibcurlMimeExchange.cs -- <case> <url>`, P/Invoke only, no package) that runs one case through `curl_mime_*` and `curl_easy_perform` and exits with the CURLcode, and drive it with `Record-CurlExchange.ps1 -Curl dotnet -CurlArgs run,Record-LibcurlMimeExchange.cs,--,<case>,http://127.0.0.1:<port>/` so the loopback server records the request.

Cases: (1) a part `curl_mime_filedata(part, "nope.txt")` plus a data part; (2) a part with data "h\xC3\xA9llo" and `curl_mime_encoder(part, "7bit")`; (3) a part with `curl_mime_subparts` (one data subpart) and `curl_mime_encoder(part, "base64")`, Stewart's harness gave a 449-byte body, ours 297; (4) case (1)'s data part alone with `Transfer-Encoding: chunked` in CURLOPT_HTTPHEADER.

## Acceptance criteria

- [ ] `Record-LibcurlMimeExchange.cs` runs each of the four cases against libcurl-4.dll and exits with the CURLcode.
- [ ] `Documentation/Product/LibcurlMimeEdgeCaseRecordings.md` holds, per case, the exact code, the recorded request bytes (with chunk framing for case 4) and the body length.

## Notes

## Log

- 2026-10-22: Created.
