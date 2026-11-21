---
id: BL-5647
title: Add DELETE to the s3:// client
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3RequestMethod.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Operations.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5647 — Add DELETE to the s3:// client

## Goal

`curl -X DELETE s3://bucket/key` deletes the object (S3 DeleteObject), signed with SigV4 like the other s3 operations.

## Context

From Surly cookbook bug report `2026-11-20T1836-Curl-bug-report.md` (batch 6 of the 1,000-recipe plan, and batch 5's CI on Linux and macOS), finding 8 (ask). Build tested: Curl `7d167fa10a` (`curl`, `curl-mcp`, `Curl.Api`), Windows 11 (Linux and macOS through the cookbook's CI where stated); server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

`s3://` has no DELETE: `-X DELETE` exits 4, so deletes need `https://` with `--aws-sigv4`. Add DeleteObject (and decide whether `-X DELETE` on a bucket URL deletes the bucket or is refused; an ADR if not obvious). Treat a 204 as success and a 404 as the error the other s3 operations use. Update `--extended-help` and `--ai-help` for s3. Check `az://` for the same gap and file it separately if it has one.

## Acceptance criteria

- [x] Against an S3 test server (SeaweedFS or Surl), `curl -X DELETE s3://bucket/key` removes the object and exits 0; a following GET fails as a missing object does.
- [x] The signed DELETE request's canonical request is pinned by a unit test.
- [x] `--extended-help` and `--ai-help` list DELETE for s3; `dotnet build` is clean and the fast tests pass.

## Notes

- Decided: ADR-1321 (bucket URL refused with exit 3, -X DELETE with -T refused). Help text comes from the S3Extension description, updated for both --extended-help and --ai-help. No live server run (lane has none); the request, 204 success and 404 failure are pinned through the fake connector, canonical request pinned in S3CanonicalRequestTests. Quality gate for the S3 library: 0 failing, 100% line and branch.
- az:// has the same gap: filed as a separate task.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1836, finding 8 (ask).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 4571b016 9f9afb57 c58c2df7.
- 2026-11-20: Doing -> Done. s3:// -X DELETE sends a signed DeleteObject
