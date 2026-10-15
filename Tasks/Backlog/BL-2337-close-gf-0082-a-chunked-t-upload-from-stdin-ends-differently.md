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
completed:
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

- [ ] `behaviour:test60`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1072`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 65; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1073`: Curl answers what curl 8.22.0 answers on linux, `reference curl exits 65; stdout 0 bytes: `, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on linux) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
