---
id: BL-300
title: Pass the -F ;encoder= value through to MultipartFormPart.Encoder in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-233, BL-274]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-300 — Pass the -F ;encoder= value through to MultipartFormPart.Encoder in Curl.Console

## Goal

`curl -F "t=hi;encoder=base64" <url>` sends the part base64-encoded with `Content-Transfer-Encoding: base64`, and `-F "t=hi;encoder=bogus"` exits 43 with `curl: (43) A libcurl function was given a bad argument`.

## Context

- Found while delivering BL-274 (2026-09-26): `MultipartFormBodyBuilder` honours `MultipartFormPart.Encoder` (ADR-0041), and `FormPartSpecification.Encoder` (BL-189) carries the parsed value, but nothing maps one to the other yet; BL-233 wires `-F` through the builder.
- Set `Encoder = specification.Encoder` where `Curl.Console` turns a `FormPartSpecification` into a `MultipartFormPart`.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test runs `-F "t=hi;encoder=base64"` against a fake connection and sees `Content-Transfer-Encoding: base64` and the body `aGk=`.
- [x] A test runs `-F "t=hi;encoder=bogus"` and sees exit 43 and `curl: (43) A libcurl function was given a bad argument` on stderr.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- 2026-09-27: `MultipartFormPartMapping.FromSpecification` now sets `Encoder = part.Encoder`; the builder already validates the name and returns exit 43 (ADR-0041), so no new decision was needed.
- Tests: `CurlCommandRunnerFormTests.RunAsync_FormBase64Encoder_SendsTheBase64EncodedPart` pins the whole request (187-byte body, the length curl 8.21.0 sent on 2026-09-26 per `MultipartFormBodyBuilderEncoderTests`); `RunAsync_FormUnknownEncoder_Exits43WithoutConnecting` pins the stderr measured on 2026-09-27 with `curl -sS -F "t=hi;encoder=bogus" http://127.0.0.1:1/` (curl 8.21.0, Schannel) -> `curl: (43) A libcurl function was given a bad argument`, exit 43; `MultipartFormPartMappingTests.FromCommandLine_PartWithEncoder_CopiesTheEncoder` covers the mapping.
- `dotnet format --verify-no-changes` reports only ENDOFLINE across the whole worktree (checkout line endings), none of it from this change.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -F ;encoder= now reaches the multipart builder: base64 parts are sent encoded and an unknown encoder exits 43
