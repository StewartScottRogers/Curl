---
id: BL-2371
title: Close GF-0095: A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0 (on macos)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2371 — Close GF-0095: A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0 (on macos)

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0095 (A chunked -T - upload that waited for 100 Continue sends its closing chunk in a separate write, so Curl exits 56 on macOS where curl exits 0), so a later gap analysis measures each of `behaviour:test60` as `match`.

## Context

- Finding: GF-0095, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Platform: macos, measured against curl 8.22.0 built from the release tarball with OpenSSL by the gap-platforms workflow (.github/workflows/gap-platforms.yml, ADR-0505). This gap is macos's own: Windows does not show it.
- Lanes test only on Windows, so a lane cannot see this gap pass locally. The proof is the gap-platforms workflow (run it on the branch: gh workflow run gap-platforms.yml -f ref=<branch>) or the CI job on macos passing a test that pins each item; write those tests platform-neutral or mark them for macos.
- Items: `behaviour:test60`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test60 (HTTP PUT from stdin, -T - -H "Content-Length: 1", reply HTTP/1.0 200 swsclose) expected 'reference curl exits 0; stdout 0 bytes: ', actual 'exit code 56, reference 0'; evidence 'was match; in-process and out-of-process differ': the in-process harness passes it, and the macOS cross-check through the built binaries fails it. Reproduce on macOS from the repository root: pwsh -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/measurements/macos/behaviour.json -TestsData <release>/tests/data -OutFile $TMPDIR/behaviour.json, then read item behaviour:test60.

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, HttpRequestBodyWriter.StartReading sets heldBack only when EarlyResponseWatch is null, so a chunked stream body of unknown length that waited for 100 Continue (test60: curl adds Expect: 100-continue to the -T - upload, the 1-second wait runs out) still writes its data chunk through HttpContinueWaitConnection.SendUnlessStoppedAsync and then 0\r\n\r\n in a write of its own. The sws-like server reads the one byte Content-Length promises after the first write, answers and closes; the closing chunk then meets a closed socket and macOS resets the connection, so the response read fails with exit 56. Extend the BL-2341 hold-back to the early-response path: hold each framed piece until the next read and send the last data chunk and the closing chunk in one SendUnlessStoppedAsync call (keeping the 300-or-above stop and CutShort), as curl fills its upload buffer to end of stream before it sends. Add a focused unit test in Curl.Protocol.Http.UnitTests with a fake IConnection that records writes: an Expect: 100-continue chunked upload of unknown length whose wait runs out gives one write holding '13\r\nmore than one byte\n\r\n0\r\n\r\n' (ADR-0482). Explained by ADR-0525: its decision covers only bodies 'sent without waiting for 100 Continue' and leaves 'bodies sent after 100 Continue (whose early-response watch sends piece by piece)' unchanged, which is exactly test60's path; the macOS exit 56 the ADR's context names is therefore still open. The reset-on-late-write is a BSD socket behaviour, which is why only macOS shows it.

## Acceptance criteria

- [ ] `behaviour:test60`: Curl answers what curl 8.22.0 answers on macos, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] The gap-platforms workflow run on the branch (or the CI job on macos) passes for the named items; lanes test only on Windows, so a green Windows run alone does not close this task.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
