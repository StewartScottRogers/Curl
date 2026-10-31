---
id: GF-0095
title: A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0
area: behaviour
platform: macos
key: behaviour:chunked-upload-after-expect-continue-splits-closing-chunk
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-15_1657
closed: 2026-10-17_2225
regression: false
items: [behaviour:test60]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2371
tasks: [BL-2371]
---
# GF-0095 - A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0

## Summary

Curl differs from upstream curl in behaviour on macos: A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0.

## Evidence

behaviour:test60 (HTTP PUT from stdin, -T - -H "Content-Length: 1", reply HTTP/1.0 200 swsclose) expected 'reference curl exits 0; stdout 0 bytes: ', actual 'exit code 56, reference 0'; evidence 'was match; in-process and out-of-process differ': the in-process harness passes it, and the macOS cross-check through the built binaries fails it. Reproduce on macOS from the repository root: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/macos/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json, then read item behaviour:test60.

## Suggestion

In Curl.Protocol.Http.UnitLibrary, HttpRequestBodyWriter.StartReading sets heldBack only when EarlyResponseWatch is null, so a chunked stream body of unknown length that waited for 100 Continue (test60: curl adds Expect: 100-continue to the -T - upload, the 1-second wait runs out) still writes its data chunk through HttpContinueWaitConnection.SendUnlessStoppedAsync and then 0\r\n\r\n in a write of its own. The sws-like server reads the one byte Content-Length promises after the first write, answers and closes; the closing chunk then meets a closed socket and macOS resets the connection, so the response read fails with exit 56. Extend the BL-2341 hold-back to the early-response path: hold each framed piece until the next read and send the last data chunk and the closing chunk in one SendUnlessStoppedAsync call (keeping the 300-or-above stop and CutShort), as curl fills its upload buffer to end of stream before it sends. Add a focused unit test in Curl.Protocol.Http.UnitTests with a fake IConnection that records writes: an Expect: 100-continue chunked upload of unknown length whose wait runs out gives one write holding '13\r\nmore than one byte\n\r\n0\r\n\r\n' (ADR-0482). Explained by ADR-0525: its decision covers only bodies 'sent without waiting for 100 Continue' and leaves 'bodies sent after 100 Continue (whose early-response watch sends piece by piece)' unchanged, which is exactly test60's path; the macOS exit 56 the ADR's context names is therefore still open. The reset-on-late-write is a BSD socket behaviour, which is why only macOS shows it.

## Measurements

- 2026-10-15_1657: 1 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.

## Log

- 2026-10-15_1657: Opened by gap-behaviour.
- 2026-10-15_1753: Filed BL-2371.
- 2026-10-17_2225: Closed: run 2026-10-17_2225 measured every item as match or excluded on macos.
