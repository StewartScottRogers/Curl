---
id: BL-6031
title: Fix fitness finding EXT-120: s3:// upload (-T) still rewrites an all-digit or hex bucket name into a dotted quad (PARTLY FIXED)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/UploadTransferUrl.cs, Curl.Cli.UnitLibrary/README.md, Curl.Cli.UnitTests/UploadTransferUrlTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6031 — Fix fitness finding EXT-120: s3:// upload (-T) still rewrites an all-digit or hex bucket name into a dotted quad (PARTLY FIXED)

## Goal

EXT-120 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: s3:// upload (-T) still rewrites an all-digit or hex bucket name into a dotted quad (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

This is an extension (Part B: extended protocols and extension options): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low (the upload goes to the wrong bucket; a real server refuses it) · **Confidence:** Confirmed (mock 2 runs, SeaweedFS 1) · **Area:** s3, Windows build.
- **What is fixed:** gs, and s3 GET/list/DELETE, now keep the bucket name.
- **Server:** `docker run -d --name sw -p 127.0.0.1:27300:8333 -v <dir>:/cfg chrislusf/seaweedfs:4.54 server -s3 -s3.config=/cfg/s3.json` with an identity `AKIDS4`/`s4secret` (Admin); create the bucket with `curl -X PUT --aws-sigv4 aws:amz:us-east-1:s3 -u AKIDS4:s4secret http://127.0.0.1:27300/12345678`.
- **Repro:** `AWS_ENDPOINT_URL=http://127.0.0.1:27300 AWS_ACCESS_KEY_ID=AKIDS4 AWS_SECRET_ACCESS_KEY=s4secret AWS_REGION=us-east-1 curl -sv -T up.txt s3://12345678/k` (also `s3://0x10/k`, `s3://127.1/k`, and `s3://12345678/`, which appends the file name).
- **Expected:** `--extended-help` documents `s3://bucket/key`; a GET of the same URL already addresses bucket `12345678`. The upload should go to the same bucket.
- **Actual (.NET):** `> PUT /0.188.97.78/k`, `< HTTP/1.1 400`, exit 22; a later GET of `s3://12345678/k` is 404 NoSuchKey. Against a logging mock the request is `PUT /0.188.97.78/k` with exit 0; `0x10` → `/0.0.0.16/k`, `127.1` → `/127.0.0.1/k`.
- **Likely location:** the `-T` path rebuilds the URL (file-name append, normalisation) before the S3 handler runs, so `context.Url.OriginalString` reaching `S3Endpoint.BucketOf()` (`Curl.Extended.Protocol.S3.UnitLibrary/Client/S3ProtocolHandler.cs:29`) already holds the dotted quad. Keep the URL's original authority text on the upload path.
- **Acceptance check:** the SeaweedFS upload returns 200 and `s3://12345678/k` then downloads `up.txt` byte-identical.

## Acceptance criteria

- [x] the SeaweedFS upload returns 200 and `s3://12345678/k` then downloads `up.txt` byte-identical.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: `UploadTransferUrl` (Curl.Cli) rewrote a `-T` URL with `CurlUrl.Host`, so the handler never saw the typed bucket. The fix is there, not in `S3ProtocolHandler.cs`; `touches` changed to match. No other task in Doing on origin/work/dark-factory named those files. `gs://` uploads had the same cause and are fixed too (ADR-1425).
- Acceptance check run against chrislusf/seaweedfs:4.54: `-T` to `s3://12345678/k`, `s3://0x10/k`, `s3://127.1/k` and `s3://12345678/` each returned 200 and downloaded byte-identical.
- `%{url_effective}` still prints the dotted quad: it is normalised separately, for downloads as for uploads, and does not affect where the request goes.
- The pinning test is in `Curl.Cli.UnitTests`, the fixed library's tests (ADR-0482). Tests are pure string work, so platform-neutral.
- Quality run on Curl.Cli.UnitLibrary: 100% line and branch; its one failing member (`HostToWrite`, complexity 12) was then split into two methods. Build, Curl.Cli.UnitTests and format are clean after the split; quality was not rerun.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: e3d9353f 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. -T keeps an s3 or gs bucket name as typed
