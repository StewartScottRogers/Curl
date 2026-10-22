---
id: BL-3022
title: Land the 8.22.0 --libcurl setopt order: fix the Console order expectation and commit with the Cli tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/LibcurlSourceCode.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProtocolOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.ProxyTlsAndAuthentication.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferOptions.cs, Curl.Cli.UnitLibrary/LibcurlSourceCode.TransferFiles.cs, Curl.Cli.UnitTests/LibcurlSourceCodeOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProxyTlsAndAuthenticationTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTransferFileTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeTransferOptionTests.cs, Curl.Cli.UnitTests/LibcurlSourceCodeProtocolOptionTests.cs, Curl.Console.UnitTests/CurlCommandRunnerLibcurlTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3022 — Land the 8.22.0 --libcurl setopt order: fix the Console order expectation and commit with the Cli tests

## Goal

`curl --libcurl` writes its `curl_easy_setopt` lines in the order of curl 8.22.0's `src/config2setopts.c`, with the Cli tests and the Console test that pinned the old order updated and the six newly passing upstream cases on the ratchet, all committed.

## Context

Split from BL-2966 (its Context and Notes in `Tasks/Deferred/BL-2966-*.md` give the 8.22.0 order, measured with real curl 8.22.0). The reorder of `LibcurlSourceCode` and its Cli tests is done and sits in a stash that the dark factory applies for this task: stash `83574a174dfbd4d6be285ab0aad41731a6c7c6b5` (11 files: the five `LibcurlSourceCode*.cs` files, five Cli test files with 94 order expectations changed and 5 renames, the focused test in `LibcurlSourceCodeProtocolOptionTests.cs` that pins `CURLOPT_USERPWD` before `CURLOPT_DIRLISTONLY` and `CURLOPT_FTP_SKIP_PASV_IP` after `CURLOPT_PREQUOTE`, and six added lines in `PassingUpstreamCases.txt` for 1402 and 1404 to 1407 and 1465). The Cli tests passed and cases 1400 to 1407, 1420, 1465 and 1481 passed. If the shift reports it did not apply, `git diff <sha>^1 <sha>` shows the tracked changes.

Left in this task: `Curl.Console.UnitTests/CurlCommandRunnerLibcurlTests.cs`, test `RunAsync_LibcurlWithAnUploadFile_WritesItsUrlUploadAndSize`, still expects the old order: change it to 8.22.0's. Then build, run the fast tests of `Curl.Cli.UnitTests` and `Curl.Console.UnitTests` and commit. The quality measurement and the CI check are BL-3023.

## Acceptance criteria

- [x] `LibcurlSourceCode`'s doc comments name 8.22.0, and the order follows `config2setopts.c` (proxy, credentials and headers, transfer, http/ftp, ssh, tls, dns and network, mail and sasl, misc).
- [x] A test in `LibcurlSourceCodeProtocolOptionTests.cs` pins `CURLOPT_USERPWD` before `CURLOPT_DIRLISTONLY` and `CURLOPT_FTP_SKIP_PASV_IP` after `CURLOPT_PREQUOTE`.
- [x] `RunAsync_LibcurlWithAnUploadFile_WritesItsUrlUploadAndSize` expects the 8.22.0 order and passes.
- [x] Cases 1400 to 1407, 1420, 1465 and 1481 pass with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows, and 1402, 1404, 1405, 1406, 1407 and 1465 are on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`, `Curl.Console.UnitTests` and `Curl.Conformance.UnitTests`.

## Notes

- 2026-10-21: Split from BL-2966. The stash was re-stored under this task's ID (same commit 83574a174dfbd4d6be285ab0aad41731a6c7c6b5) so the shift applies it on the first claim.

- 2026-10-21 (lane 5): Applied stash 83574a17 with `git diff | git apply --3way` (clean). Changed `RunAsync_LibcurlWithAnUploadFile_WritesItsUrlUploadAndSize` to 8.22.0's order: URL, NOPROGRESS, USERAGENT (headers), then UPLOAD and MAXREDIRS (transfer), INFILESIZE_LARGE last; the agent's version is left open so the test does not pin the product version. Build -warnaserror clean; Cli 3967, Console 2825, Conformance 4034 passed; cases 1400-1407, 1420, 1465, 1481 each passed with CURL_UPSTREAM_CASE. The complexity gate is enforced by the clean build; the coverage measurement is BL-3023, as this task says.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 8869e893 8598eb64 445fd6a6 e0ff5a3e 97abd8b3.
- 2026-10-21: Doing -> Done. --libcurl writes its setopt lines in curl 8.22.0's config2setopts.c order; Console and Cli tests updated; 1402, 1404-1407, 1465 on the ratchet
