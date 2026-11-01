---
id: GF-0082
title: A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65
area: behaviour
platform: linux
key: behaviour:stdin-chunked-upload-unix-diverges
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-15_0751
closed: 2026-10-15_1657
regression: false
items: [behaviour:test60, behaviour:test1072, behaviour:test1073]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
task: BL-2337
tasks: [BL-2337]
---
# GF-0082 - A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65

## Summary

Curl differs from upstream curl in behaviour on linux: A chunked -T - upload from stdin ends differently from the reference curl on Linux: the request differs at the terminating chunk, and a 401/301 retry exits 25 where curl exits 65.

## Evidence

All three cases were match in-process. The out-of-process cross-check (Measure-ReferenceCrossCheck.ps1) then disagreed. behaviour:test60 (-T - -H 'Content-Length: 1'): expected 'reference curl exits 0; stdout 0 bytes', actual 'request differs at byte 179'. Byte 179 is where the terminating 0-chunk starts, right after the '13' chunk holding 'more than one byte'. behaviour:test1072 (-T - -u testuser:testpass --anyauth, server answers HTTP/1.0 401) and behaviour:test1073 (-T - -L, server answers HTTP/1.0 301): expected 'reference curl exits 65; stdout 0 bytes', actual 'exit code 25, reference 65'. The same three items are gaps on macOS too, where test60 reads 'exit code 56, reference 0'. On Windows all three match. Reproduce on linux from the repository root: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/linux/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json (it runs both binaries through Record-CurlExchange.ps1, which writes the case's <stdin> and closes it).

## Suggestion

Make Curl's chunked upload of an unseekable stdin finish on Linux and macOS the way the reference curl's does. Curl must read stdin to EOF and send the last 0-chunk before it reads the server's answer. Start in Curl.Protocol.Http.UnitLibrary HttpRequestBodyWriter.WriteStreamAsync/WriteAsync, where LastChunk is written, and the Unix stdin stream Curl.Console/Program.cs opens with Console.OpenStandardInput. Find out why the final chunk is not on the wire before the server's 3-second idle answer on Unix (Linux records no last chunk; macOS gets exit 56 writing after the server closed). Then check the retry decision in HttpProtocolHandler. MayRetry/ThrowIfChunkedResendToHttp10 return exit 25 'Chunky upload is not supported by HTTP 1.0' for every chunked resend to an HTTP/1.0 server. The reference instead fails with exit 65 through the rewind path (UploadRewindFailed) once the whole stdin body has been sent and must be rewound for the --anyauth or -L retry. Choose 65 over 25 when the body was fully consumed, and pin each platform's answer in its own unit test if the Windows reference keeps answering 25. Add focused unit tests in Curl.Protocol.Http.UnitTests (ADR-0482). Context: ADR-0033 (a followed PUT redirect passes stdin on as it is) and ADR-0036 (body framing) are the standing decisions this touches.

## Measurements

- 2026-10-15_0751: 3 of 3 items are gaps.
- 2026-10-15_1657: 0 of 3 items are gaps.
- 2026-10-17_2225: 0 of 3 items are gaps.
- 2026-10-19_0804: 0 of 3 items are gaps.
- 2026-10-19_1329: 0 of 3 items are gaps.
- 2026-10-22_0148: 0 of 3 items are gaps.
- 2026-10-23_1637: 0 of 3 items are gaps.
- 2026-10-25_1542: 0 of 3 items are gaps.
- 2026-10-30_2103: 0 of 3 items are gaps.
- 2026-10-31_1828: 0 of 3 items are gaps.

## Log

- 2026-10-15_0751: Opened by gap-behaviour.
- 2026-10-15_0900: Filed BL-2337.
- 2026-10-15_1657: Closed: run 2026-10-15_1657 measured every item as match or excluded on linux.
