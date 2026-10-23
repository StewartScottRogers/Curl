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
completed: 2026-10-22
---
# BL-3210 — Record upstream libcurl 8.22.0 request bytes and codes for four MIME edge cases through a P/Invoke harness

## Goal

The request bytes and final codes upstream libcurl 8.22.0 gives for BL-3208's four MIME edge cases are recorded, so BL-3211 to BL-3214 pin measured values.

## Context

Split from BL-3208 (Stewart's Libcurl API conformance batch, report 30). The curl tool cannot measure these: `-F a=@nope.txt` and `-F "a=<file;encoder=7bit"` over non-ASCII both exit 26 before connecting (the tool's own checks, measured 2026-10-22 with curl 8.22.0 from `/ucrt64/bin`), and the tool applies no encoder to a `=(` multipart part. They need libcurl called directly. `/ucrt64/bin/libcurl-4.dll` is libcurl 8.22.0 (Schannel): write a C# file-based app (`dotnet run Record-LibcurlMimeExchange.cs -- <case> <url>`, P/Invoke only, no package) that runs one case through `curl_mime_*` and `curl_easy_perform` and exits with the CURLcode, and drive it with `Record-CurlExchange.ps1 -Curl dotnet -CurlArgs run,Record-LibcurlMimeExchange.cs,--,<case>,http://127.0.0.1:<port>/` so the loopback server records the request.

Cases: (1) a part `curl_mime_filedata(part, "nope.txt")` plus a data part; (2) a part with data "h\xC3\xA9llo" and `curl_mime_encoder(part, "7bit")`; (3) a part with `curl_mime_subparts` (one data subpart) and `curl_mime_encoder(part, "base64")`, Stewart's harness gave a 449-byte body, ours 297; (4) case (1)'s data part alone with `Transfer-Encoding: chunked` in CURLOPT_HTTPHEADER.

## Acceptance criteria

- [x] `Record-LibcurlMimeExchange.cs` runs each of the four cases against libcurl-4.dll and exits with the CURLcode.
- [x] `Documentation/Product/LibcurlMimeEdgeCaseRecordings.md` holds, per case, the exact code, the recorded request bytes (with chunk framing for case 4) and the body length.

## Notes

- Libcurl DLL is Git for Windows's `C:Program FilesGitCrt64binibcurl-4.dll` (8.22.0, Schannel); the harness takes an optional third argument for another path. Case 1 (missing file) gives code 0 with an empty part, case 2 gives 26, case 3 body is 483 bytes with this harness, case 4 chunk is 0x99. Part names a/b are my choice.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 5ef7e827 6f47a55a.
- 2026-10-22: Doing -> Done. Four cases recorded
