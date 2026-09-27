---
id: BL-298
title: Pass the -F ;encoder= value through to MultipartFormPart.Encoder in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-233, BL-274]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-298 — Pass the -F ;encoder= value through to MultipartFormPart.Encoder in Curl.Console

## Goal

`curl -F "t=hi;encoder=base64" <url>` sends the part base64-encoded with `Content-Transfer-Encoding: base64`, and `-F "t=hi;encoder=bogus"` exits 43 with `curl: (43) A libcurl function was given a bad argument`.

## Context

- Found while delivering BL-274 (2026-09-26): `MultipartFormBodyBuilder` honours `MultipartFormPart.Encoder` (ADR-0040), and `FormPartSpecification.Encoder` (BL-189) carries the parsed value, but nothing maps one to the other yet; BL-233 wires `-F` through the builder.
- Set `Encoder = specification.Encoder` where `Curl.Console` turns a `FormPartSpecification` into a `MultipartFormPart`.

## Acceptance criteria

- [ ] A `Curl.Console.UnitTests` test runs `-F "t=hi;encoder=base64"` against a fake connection and sees `Content-Transfer-Encoding: base64` and the body `aGk=`.
- [ ] A test runs `-F "t=hi;encoder=bogus"` and sees exit 43 and `curl: (43) A libcurl function was given a bad argument` on stderr.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
