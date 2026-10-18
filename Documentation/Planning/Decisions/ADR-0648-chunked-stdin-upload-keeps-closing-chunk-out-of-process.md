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
  its `Content-Length: 1` promises has arrived, so it can answer between curl's two writes.
  test60's response holds `swsclose`, so the cross-check does not hold the connection open: the
  recorder closes it at once, and curl's closing chunk, sent after that, is not recorded (BL-2522
  corrected an earlier reading here that the recorder answered it as a held request). Whether
  curl's two writes reach the recorder's first read together depends on how the runner schedules
  the two processes.

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
- Re-measured by BL-2522 in gap-platforms run 39063808349, on commit
  `777f9cf84c9a4cf4c210ac4d45ac53591f08b4ea` (BL-2483's commits included): `behaviour:test60` is
  still a gap on Linux and on macOS, "request differs at byte 179". The run uploads no
  recordings. Byte 179 is where the reference's request stops: test60's 154-byte head plus curl's
  first 25-byte write, the data chunk. Curl's single write of the data chunk and the closing chunk
  cannot end there. So the shorter `request.bin` is the reference's, and the cause is the
  recorder. `Record-CurlExchange.ps1`'s `Test-RequestComplete` takes `Content-Length` before
  `Transfer-Encoding: chunked`. Upstream's `tests/server/sws.c` (curl 8.22.0) reads a chunked
  request to its `\r\n0\r\n\r\n` whatever `Content-Length` says. BL-2647 makes the recorder do the
  same, and the next gap run's re-measurement closes the difference. Curl is unchanged.
- Re-measured after BL-2647 in gap-platforms run 39079451070, dispatched from `work/dark-factory`
  so the gap office's tools carry the fixed recorder, on commit
  `bc4d88fbde7e060af4b8a9445afdacefe1fa7d83`: `behaviour:test60` is `match` on Linux and on
  macOS, and no other behaviour case changed state. A run dispatched from `master` measures with
  `master`'s recorder (run 39076579396 still found the gap), so the scheduled run agrees only
  once BL-2647 reaches `master`.
