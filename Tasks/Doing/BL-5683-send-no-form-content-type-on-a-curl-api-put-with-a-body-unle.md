---
id: BL-5683
title: Send no form Content-Type on a Curl.Api PUT with a body unless the body names one
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/RequestBodies/CurlContent.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5683 — Send no form Content-Type on a Curl.Api PUT with a body unless the body names one

## Goal

A `Curl.Api` HTTP `PUT` with a body sends the body's own content type, or none, never `application/x-www-form-urlencoded` by default, matching `curl -T`.

## Context

From Surly cookbook bug report `2026-11-21T0547-Curl-bug-report.md` (batch 9 of the 1,000-recipe plan), finding 4. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

A `Curl.Api` `PUT` with a `Body` sends `Content-Type: application/x-www-form-urlencoded`; `curl -T f` sends none. The form type comes from libcurl's POSTFIELDS default (`curl -X PUT -d data` sends it too), so a PUT body is being sent as post fields under a custom method. Decide (ADR, decided by Claude under Stewart's delegation; BL-5594 kept HTTP POST and PUT "exactly as before", so this changes it on purpose): a PUT body is an upload (`CURLOPT_UPLOAD` with `CURLOPT_INFILESIZE_LARGE`, as `-T`), with the content type only when `CurlContent` carries one. POST keeps the form default, as `curl -d` does.

## Acceptance criteria

- [ ] An ADR records how an HTTP PUT body is sent and why.
- [ ] A `Curl.Api` PUT with a stream body sends no `Content-Type`, and one with a `CurlContent` that names a type sends that type, pinned by tests of the request bytes.
- [ ] POST with a body still sends `application/x-www-form-urlencoded` unless the body names a type, pinned by the existing tests.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0547, finding 4.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 4f977388 e2dc41ea 07c16a66 3d836d5e.
