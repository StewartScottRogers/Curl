---
id: BL-2337
title: Close GF-0082: A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65 (on linux)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2337 — Close GF-0082: A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65 (on linux)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0082 (A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65), so a later gap analysis measures each of `behaviour:test60`, `behaviour:test1072`, `behaviour:test1073` as `match`.

## Context

- Finding: GF-0082, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Platform: linux, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is linux's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on linux passing a test that pins each item; write those tests platform-neutral or mark them for linux.
- Items: `behaviour:test60`, `behaviour:test1072`, `behaviour:test1073`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

All three cases were match in-process. The out-of-process cross-check (Measure-ReferenceCrossCheck.ps1) then disagreed. behaviour:test60 (-T - -H 'Content-Length: 1'): expected 'reference curl exits 0; stdout 0 bytes', actual 'request differs at byte 179'. Byte 179 is where the terminating 0-chunk starts, right after the '13' chunk holding 'more than one byte'. behaviour:test1072 (-T - -u testuser:testpass --anyauth, server answers HTTP/1.0 401) and behaviour:test1073 (-T - -L, server answers HTTP/1.0 301): expected 'reference curl exits 65; stdout 0 bytes', actual 'exit code 25, reference 65'. The same three items are gaps on macOS too, where test60 reads 'exit code 56, reference 0'. On Windows all three match. Reproduce on linux from the repository root: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/linux/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json (it runs both binaries through Record-CurlExchange.ps1, which writes the case's <stdin> and closes it).

Suggestion, copied from the finding:

Make Curl's chunked upload of an unseekable stdin finish on Linux and macOS the way the reference curl's does. Curl must read stdin to EOF and send the last 0-chunk before it reads the server's answer. Start in Curl.Protocol.Http.UnitLibrary HttpRequestBodyWriter.WriteStreamAsync/WriteAsync, where LastChunk is written, and the Unix stdin stream Curl.Console/Program.cs opens with Console.OpenStandardInput. Find out why the final chunk is not on the wire before the server's 3-second idle answer on Unix (Linux records no last chunk; macOS gets exit 56 writing after the server closed). Then check the retry decision in HttpProtocolHandler. MayRetry/ThrowIfChunkedResendToHttp10 return exit 25 'Chunky upload is not supported by HTTP 1.0' for every chunked resend to an HTTP/1.0 server. The reference instead fails with exit 65 through the rewind path (UploadRewindFailed) once the whole stdin body has been sent and must be rewound for the --anyauth or -L retry. Choose 65 over 25 when the body was fully consumed, and pin each platform's answer in its own unit test if the Windows reference keeps answering 25. Add focused unit tests in Curl.Protocol.Http.UnitTests (ADR-0482). Context: ADR-0033 (a followed PUT redirect passes stdin on as it is) and ADR-0036 (body framing) are the standing decisions this touches.

## Acceptance criteria

- [x] `behaviour:test60`: moved to BL-2340, because it does not reproduce outside the gap harness (see Notes).
- [x] `behaviour:test1072`: Curl answers what curl answers on linux, exit 65 with 0 bytes on stdout.
- [x] `behaviour:test1073`: Curl answers what curl answers on linux, exit 65 with 0 bytes on stdout.
- [x] The CI job on linux runs the platform-neutral tests that pin both platforms' answers. Linux behaviour was also checked against real curl under WSL (see Notes). The finding itself closes only when a gap analysis re-measures it.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- **Cause of test1072 and test1073 (measured 2026-10-15).** Measured with WSL Ubuntu's `/usr/bin/curl` 8.18.0 (OpenSSL) against an `nc` server answering HTTP/1.0 401 or 301:
  - stdin as a pipe exits 65: `seek callback returned error 2`, then `rewind of client reader 'cr-in' failed: 65`, with no new connection.
  - stdin as a redirected file exits 25.
  - curl rewinds stdin with `lseek` before it builds the resend. That fails on a pipe off Windows but not on Windows, which is why the Windows reference answers 25.
- **Decision (ADR-0520).**
  - New `IRewindableUpload` in `Curl.Protocol.Http`.
  - `HttpProtocolHandler.FailureBeforeConnecting` now fails a chunked resend to HTTP/1.0 with `UploadRewindFailed` (exit 65) when the body cannot be rewound.
  - `Curl.Console`'s `StandardInputUploadStream.CanRewind` is true when the wrapped stream seeks, or on Windows.
  - So the in-process upstream cases (seekable `MemoryStream` stdin) and Windows keep 25, and the conformance ratchet still holds 1072 and 1073 on every platform.
- **Verified on Linux.** A self-contained `linux-x64` publish run in WSL with stdin piped now prints the same `-v` rewind lines and `curl: (65) seek callback returned error 2` as real curl, for both the 401 and the 301.
- **Left as they are (ADR-0520 Consequences).**
  - Curl writes `Issue another request` before the rewind lines, where curl writes it after.
  - A file redirected into Curl's stdin off Windows still gets 65, because `UnixConsoleStream` never seeks.
- **test60.** It did not reproduce outside the gap harness: the WSL publish sent the closing `0` chunk exactly as curl did, both from a shell pipe and with stdin written late. Filed as BL-2340.
- **Quality.** `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary,Curl.Console` flagged `FailureBeforeConnecting` at complexity 14, with 100% line and branch coverage. Splitting it into `RewindFails` and `CannotRewind` brought every method under the gate.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. A chunked -T - resend from a piped stdin to an HTTP/1.0 server fails its rewind with exit 65 off Windows, as curl does (test1072, test1073); test60 split to BL-2340
