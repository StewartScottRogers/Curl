# ADR-0648: A chunked `-T -` upload read from a pipe keeps sending its closing chunk; test60's out-of-process difference is the recorder's race

- Status: Accepted
- Date: 2026-10-16
- Task: BL-2483 (gap-platforms run 38912832952, `behaviour:test60` on Linux and macOS)
- Extends: ADR-0525, ADR-0546
- Decided by Claude under Stewart's delegation.

## Context

The gap office's reference cross-check runs upstream test60
(`-T - -H "Content-Length: 1"`, stdin `more than one byte\n`) through Curl and the platform's
curl with `Record-CurlExchange.ps1 -RequestReadMilliseconds 3000 -HoldOpenMilliseconds 1500
-AnswerHeldRequests 20`, and on Linux and macOS found the two requests differing at byte 179:
where the closing `0\r\n\r\n` starts. The task assumed Curl was the side without it.

Measured on 2026-10-16 (BL-2483 Notes), with stdin on a real pipe:

- Windows, curl 8.22.0 (Git for Windows' ucrt64 build) and the built Curl.Console: both send
  184 request bytes, the closing chunk included; byte for byte the same.
- Linux (WSL Ubuntu, curl 8.18.0 OpenSSL, and Curl.Console published for `linux-x64`, stdin from
  `printf ... |`): both send 186 bytes (a longer host), the closing chunk included.
- Curl sends the data chunk and `0\r\n\r\n` in one 30-byte write once the pipe's end is read
  (ADR-0546). curl 8.22.0 sends them in two writes, 25 bytes then 5 (`Curl_xfer_send(len=25)`,
  then `sending last upload chunk of 5 bytes`). The recorder answers as soon as the one body byte
  its `Content-Length: 1` promises has arrived, so it answers between curl's two writes and then
  answers the lone `0\r\n\r\n` again as a held request; curl's stdout carries that second answer,
  Curl's does not. Which side's closing chunk the recorder sees before the exchange ends depends
  on how the runner schedules the two processes.

## Decision

Curl keeps sending a chunked stream body of unknown length as ADR-0525 and ADR-0546 decided: the
last data chunk and the closing chunk in one write, once a read of the stream gives nothing,
whatever the stream is. Upstream test60's expected protocol has the closing chunk, curl sends it
on every platform measured here, and a pipe that gives its bytes and its end in separate reads is
pinned by `HttpRequestBodyWriterTests.WriteAsync_ChunkedStdinOnAPipeAfterTheContinueWait_SendsDataAndLastChunkInOneWriteOnceThePipeCloses`.
Curl does not split the write to copy curl's two sends: that would bring back the reset GF-0095
recorded on macOS when the server answers and closes between them.

## Consequences

- No product change for BL-2483. The cross-check difference closes only when a gap-platforms run
  re-measures `behaviour:test60` as `match`; the interactive follow-up filed by BL-2483 runs it.
- If that run still differs, the cross-check's recorder settings (answering on the first body
  byte of a request whose `Content-Length` contradicts its chunked coding) are what to look at,
  in the gap office, not Curl.
