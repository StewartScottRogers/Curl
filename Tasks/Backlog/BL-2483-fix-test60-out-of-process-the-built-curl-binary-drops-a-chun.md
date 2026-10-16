---
id: BL-2483
title: Fix test60 out of process: the built curl binary drops a chunked -T - upload's closing chunk at request byte 179 on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2483 — Fix test60 out of process: the built curl binary drops a chunked -T - upload's closing chunk at request byte 179 on Linux and macOS

## Goal

Run as a real process with standard input on a pipe, `curl http://127.0.0.1:<port>/bzz/60 -T - -H "Content-Length: 1"` with stdin `more than one byte\n` sends the same request bytes as curl 8.22.0 (OpenSSL build), the closing `0\r\n\r\n` included, on Linux and macOS.

## Context

- Found by BL-2342 and BL-2372: gap-platforms run https://github.com/StewartScottRogers/Curl/actions/runs/38912832952 on `work/dark-factory` 4e63a4fd9ca02affcad51bd4bee1eff1c5bc8fa6 (BL-2341 and BL-2371 both in it). On both Linux and macOS `behaviour:test60` is `gap` with evidence `was match; in-process and out-of-process differ`: the in-process harness passes, but `Gap/Tools/Measure-ReferenceCrossCheck.ps1`, running both binaries through `Record-CurlExchange.ps1`, finds the exit codes equal (reference exits 0, stdout 0 bytes) and `request differs at byte 179`.
- Byte 179 is where curl's closing chunk starts: the head (`PUT`, `Host`, `User-Agent`, `Accept`, `Transfer-Encoding: chunked`, `Content-Length: 1`, `Expect: 100-continue`, the empty line) is 154 bytes with a 5-digit port, then `13\r\n` + `more than one byte\n` + `\r\n` ends at 179. So the out-of-process binary still sends no `0\r\n\r\n` before the server answers and closes, the symptom GF-0082 first recorded (Linux, byte 179). macOS no longer exits 56 (GF-0095's symptom): its exit code now agrees.
- Upstream case: `tests/data/test60` in curl 8.22.0. Server reply `HTTP/1.0 200 OK swsclose`. The cross-check's recorder settings: `-RequestReadMilliseconds 3000 -HoldOpenMilliseconds 1500 -AnswerHeldRequests 20`, stdin given with `-StandardInput`.
- ADR-0525 (BL-2341) and ADR-0546 (BL-2371) send the last data chunk and the closing chunk in one write, but `HttpRequestBodyWriter.EndsBody` learns the end of a body of unknown length only from a read that returns 0. In process the harness's stdin is a memory stream; out of process it is `Curl.Console/StandardInputUploadStream.cs` over a real pipe. Start by recording both binaries locally with `Record-CurlExchange.ps1` and the settings above, on Linux or macOS if one is to hand, and find whether Curl's second read of stdin returns 0, blocks, or comes after the server's answer.

## Acceptance criteria

- [ ] A focused unit test in `Curl.Protocol.Http.UnitTests` or `Curl.Console.UnitTests` drives the case the out-of-process run hits (stdin that gives its bytes and its end in separate reads, as a pipe does) and checks the data chunk and `0\r\n\r\n` reach the connection before the response is read (ADR-0482).
- [ ] `Record-CurlExchange.ps1` with the cross-check's settings records the same request bytes for the built Curl.Console and the platform's curl on Windows; the recordings are named under Notes.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [ ] An interactive follow-up task re-measures `behaviour:test60` with `gh workflow run gap-platforms.yml -f ref=<full 40-character commit>` after this lands, filed with the board script and named under Notes.

## Notes

## Log

- 2026-10-16: Created.
