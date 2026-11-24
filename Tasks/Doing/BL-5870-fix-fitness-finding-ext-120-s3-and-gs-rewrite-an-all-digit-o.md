---
id: BL-5870
title: Fix fitness finding EXT-120: s3:// and gs:// rewrite an all-digit or hex bucket name into a dotted quad, so reads, writes and lists go to a different bucket
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Endpoint.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsEndpoint.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5870 — Fix fitness finding EXT-120: s3:// and gs:// rewrite an all-digit or hex bucket name into a dotted quad, so reads, writes and lists go to a different bucket

## Goal

EXT-120 from the outside fitness review of 2026-11-24 07:08 no longer reproduces: s3:// and gs:// rewrite an all-digit or hex bucket name into a dotted quad, so reads, writes and lists go to a different bucket. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T0708` (commit `1e5d591`, the `preview` release binaries).

This is an extension (Part B): it is judged against Curl's own documentation (`curl --extended-help`, `Documentation/Protocols.md`, `Documentation/Conformance/Extended/*.md`, the ADRs), never against C curl.

The reviewer's mock servers were Python; rebuild any you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup, find the task titled with that finding ID. Check that it still reproduces on the current branch first. The finding, verbatim:


- **Severity:** Low (most real servers reject the rewritten name, but `-T` targets the wrong bucket) · **Confidence:** Confirmed (2 runs) · **Area:** s3 / gs bucket naming.
- **Mock:** an HTTPS server on 127.0.0.1:27010 logging request heads.
- **Repro:**
  - `AWS_REGION=us-east-1 curl -sS -k -u AK:SK --connect-to "::127.0.0.1:27010" -T up.txt "s3://12345678/k"`
  - `curl -sS -k --oauth2-bearer T --connect-to "::127.0.0.1:27010" "gs://0x10/k"`
- **Expected:** `--extended-help` documents `s3://bucket/key` and `gs://bucket/object`; `12345678` and `0x10` are valid bucket names. The request should target bucket `12345678` (`12345678.s3.us-east-1.amazonaws.com` or `/12345678/k`), or the port should refuse the URL.
- **Actual (.NET):** `PUT /0.188.97.78/k` with `host: s3.us-east-1.amazonaws.com`; gs sends `GET /0.0.0.16/k`; exit 0. Also `s3://[::1]/k` gives `host: [::1].s3.us-east-1.amazonaws.com`.
- **Likely location:** `Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Endpoint.cs` and the gs endpoint mapping in `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` take the bucket from the normalised `Uri.Host`; take it from the original authority text.
- **Acceptance check:** the s3 repro logs `PUT /k` with `host: 12345678.s3.us-east-1.amazonaws.com` (or `PUT /12345678/k`), never `0.188.97.78`.

## Acceptance criteria

- [ ] the s3 repro logs `PUT /k` with `host: 12345678.s3.us-east-1.amazonaws.com` (or `PUT /12345678/k`), never `0.188.97.78`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from fitness review 2026-11-24T0708.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 0f7ed03c 69116afe 3d836d5e.
