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
completed: 2026-10-16
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

- [x] A focused unit test in `Curl.Protocol.Http.UnitTests` or `Curl.Console.UnitTests` drives the case the out-of-process run hits (stdin that gives its bytes and its end in separate reads, as a pipe does) and checks the data chunk and `0\r\n\r\n` reach the connection before the response is read (ADR-0482).
- [x] `Record-CurlExchange.ps1` with the cross-check's settings records the same request bytes for the built Curl.Console and the platform's curl on Windows; the recordings are named under Notes.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Protocol.Http.UnitLibrary` reports 0 failing members.
- [x] An interactive follow-up task re-measures `behaviour:test60` with `gh workflow run gap-platforms.yml -f ref=<full 40-character commit>` after this lands, filed with the board script and named under Notes.

## Notes

- Outcome: no product defect found, so no production change. Decision recorded in ADR-0647 (decided by Claude under Stewart's delegation): Curl keeps sending the last data chunk and `0\r\n\r\n` in one write once stdin's end is read.
- Recordings (2026-10-16, scratch directories in the lane worktree, not committed), all with `-RequestReadMilliseconds 3000 -HoldOpenMilliseconds 1500 -AnswerHeldRequests 20 -StandardInput 'more than one byte\n'` and `<url>/bzz/60 -T - -H "Content-Length: 1"`:
  - Windows `.rec60\v1` (Git for Windows' ucrt64 curl 8.22.0, the recorder's default) and `.rec60\v2` (built `Curl.Console\bin\Debug\net10.0\curl.exe`): both `request.bin` 184 bytes, identical (head 154 + `13\r\nmore than one byte\n\r\n` + `0\r\n\r\n`), exit 0 both. Six more reference runs (`.rec60\r1`-`r6`, upstream's header-only reply): 184 bytes every time.
  - Linux via WSL Ubuntu (`-Curl wsl.exe -ListenAddress 172.26.96.1`; curl 8.18.0 OpenSSL and Curl.Console published `-r linux-x64 --self-contained -p:PublishAot=false`): `.rec60\w1`/`w2` (stdin through wsl.exe) and `.rec60\p1`/`p2` (stdin from a real Linux pipe, `printf ... | curl`): both binaries 186 bytes (longer host), closing chunk included, exit 0.
- What differs: curl 8.22.0's debug trace shows it sends the data chunk and the closing chunk in two writes (`Curl_xfer_send(len=25)`, then `sending last upload chunk of 5 bytes`); Curl sends 30 bytes in one. The recorder answers as soon as the one body byte `Content-Length: 1` promises arrives, i.e. between curl's two writes, then answers the lone `0\r\n\r\n` again as a held request (curl's stdout carries the second answer; Curl's does not). In CI the reference reported stdout 0 bytes, which fits the reference, not Curl, being the side whose closing chunk the recorder missed; the lane cannot read the gap office's recordings to confirm, so BL-2521 does.
- Unit test: `HttpRequestBodyWriterTests.WriteAsync_ChunkedStdinOnAPipeAfterTheContinueWait_SendsDataAndLastChunkInOneWriteOnceThePipeCloses`, with the new fake `Fakes/PipeLikeStream` (bytes on the first read, the second read waits for the writing end to close, then 0). It checks nothing is written while the end is unknown, then one 30-byte write, then the answer is read. It passed against the current code.
- Fast tests: whole solution green (exit 0); `PassingUpstreamCases.txt` untouched. Quality: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Protocol.Http.UnitLibrary` reported 0 failing members.
- Follow-up: BL-2521 (interactive only) runs `gh workflow run gap-platforms.yml -f ref=<full commit>` and records `behaviour:test60`'s outcome in ADR-0647.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: fbf9dd85 6a8f6aec 4272a6b9 8738c7f6 b51e6f31 81fa3112.
- 2026-10-16: Doing -> Done. A pipe-fed chunked -T - upload is pinned to send its data chunk and closing chunk in one write; Windows and Linux recordings match curl; ADR-0647 and BL-2521 follow up the CI cross-check
