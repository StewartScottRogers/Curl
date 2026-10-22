---
id: BL-2966
title: Write --libcurl's curl_easy_setopt lines in curl 8.22.0's config2setopts order (upstream cases 1405 and 1407)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2965]
touches: [Curl.Cli.UnitLibrary/LibcurlSourceCode.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProtocolOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProxyTlsAndAuthentication.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferFiles.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProxyTlsAndAuthenticationTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTransferFileTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTransferOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProtocolOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionCoverageTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Console.UnitTests/CurlCommandRunnerLibcurlTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2966 — Write --libcurl's curl_easy_setopt lines in curl 8.22.0's config2setopts order (upstream cases 1405 and 1407)

## Goal

`curl --libcurl` writes its `curl_easy_setopt` lines in the order curl 8.22.0's `src/config2setopts.c` sets them, so upstream cases 1405 and 1407 pass and are on `PassingUpstreamCases.txt`.

## Context

`LibcurlSourceCode` (Curl.Cli.UnitLibrary) writes the order BL-1106 measured with curl 8.21.0. curl 8.22.0 moved the tool's setopt calls into `src/config2setopts.c` (https://github.com/curl/curl/blob/curl-8_22_0/src/config2setopts.c), whose functions run in a new order: `credentials_and_headers_setopts` (`CURLOPT_XOAUTH2_BEARER`, netrc, `CURLOPT_LOGIN_OPTIONS`, `CURLOPT_USERPWD`, `CURLOPT_HTTPAUTH`, `CURLOPT_HTTPHEADER`, `CURLOPT_REFERER`, `CURLOPT_USERAGENT`, `CURLOPT_KEYPASSWD`), `transfer_setopts` (`CURLOPT_NOBODY`, `CURLOPT_FAILONERROR`, `CURLOPT_REQUEST_TARGET`, `CURLOPT_UPLOAD`, `CURLOPT_DIRLISTONLY`, ... `CURLOPT_CRLF`, the `-Q` lists, the time condition, `CURLOPT_CUSTOMREQUEST`), then `protocol_setopts` (`http_setopts`, `ftp_setopts`, `ssh_setopts`, TLS), `dns_and_network_setopts`, `mail_and_sasl_setopts` and `misc_setopts`; read the top-level caller for the exact sequence. BL-2862 measured, against the downloaded `data1405.c` and `data1407.c`:

- test1405: `CURLOPT_FTP_SKIP_PASV_IP` is written before the `-Q` lists' `CURLOPT_QUOTE`/`POSTQUOTE`/`PREQUOTE`; 8.22.0 writes it after them (ftp_setopts runs after transfer_setopts).
- test1407: `CURLOPT_USERPWD` is written after `CURLOPT_DIRLISTONLY`; 8.22.0 writes it right after `CURLOPT_URL`, before `CURLOPT_DIRLISTONLY`.

The other nine `--libcurl` cases (1400 to 1404, 1406, 1420, 1465, 1481) already match, so keep them matching. Move the whole generator to 8.22.0's order, not only these two lines, update the doc comments that say 8.21.0, and update the unit tests that pin the old order. Depends on BL-2965, which vendors the expected files the cases compare with.

## Acceptance criteria

- [ ] Each `LibcurlSourceCode` method's order follows curl 8.22.0's `config2setopts.c`, and its doc comments name 8.22.0.
- [ ] A focused unit test in `Curl.Cli.UnitTests` pins `CURLOPT_USERPWD` before `CURLOPT_DIRLISTONLY` and `CURLOPT_FTP_SKIP_PASV_IP` after `CURLOPT_PREQUOTE`, and fails without the fix (ADR-0482).
- [ ] 1405 and 1407 pass with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`; the cases BL-2965 listed still pass.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; Curl.Cli.UnitLibrary keeps 100% line and branch coverage and complexity at most 10; CI is green on Linux and macOS.

## Notes

- BL-2965 measured with the vendored files: 1402, 1404, 1406 and 1465 also fail on order, CURLOPT_USERAGENT written after POSTFIELDS, the mime parts or CURLOPT_UPLOAD instead of in credentials_and_headers_setopts. Moving the whole generator to 8.22.0 order should fix them too; add each that then passes to PassingUpstreamCases.txt. Only 1400, 1401, 1403, 1420 and 1481 are listed so far.

- 2026-10-21 (lane 8, BL-2966 run 1): generator moved to 8.22.0 order (measured with real curl 8.22.0: proxy, credentials_and_headers, transfer, http/ftp, ssh, tls, dns_and_network, mail_and_sasl, misc; misc writes GSSAPI_DELEGATION, MAIL_AUTH, SASL_AUTHZID, SASL_IR a second time). Cases 1400-1407, 1420, 1465, 1481 all pass; 1402, 1404-1407 and 1465 added to the ratchet. Curl.Cli.UnitTests updated (94 order expectations, 5 renames) and the focused test added in LibcurlSourceCodeProtocolOptionTests.cs; all pass. Left: Curl.Console.UnitTests CurlCommandRunnerLibcurlTests.RunAsync_LibcurlWithAnUploadFile_WritesItsUrlUploadAndSize pins the old order (added to touches; no Doing task names it), then the fast suite and Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary. The run ran out of its cost cap; the code is in the shift stash.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 0b924be1 61dab663 782ca8e4 7eaf8a6e.
- 2026-10-21: Doing -> Backlog. Run hit its cost cap: generator reordered and Cli tests pass (stashed); left one Curl.Console.UnitTests order expectation, the fast suite and the quality run
