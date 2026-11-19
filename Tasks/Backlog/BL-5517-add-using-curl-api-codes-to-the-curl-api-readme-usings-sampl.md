---
id: BL-5517
title: Add using Curl.Api.Codes to the Curl.Api README usings sample
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Api/README.md]
requirement: none
created: 2026-11-18
completed:
---
# BL-5517 — Add using Curl.Api.Codes to the Curl.Api README usings sample

## Goal

A reader who copies `Curl.Api/README.md`'s samples, including the usings sample, can switch on `CurlException.Code` without a compile error.

## Context

From the bug report `2026-11-18T1233-Curl-bug-report.md` (finding 2, Low), found while writing the *Surly* cookbook.

The README's `<!-- sample: usings -->` block lists `System.Net`, `Curl.Api`, `Curl.Api.Errors` and `Curl.Api.Options.Http`. But `CurlCode`, which the README uses (`CurlException` carries a `CurlCode` in `Code`), lives in `Curl.Api.Codes`. A reader who copies the samples and switches on `ex.Code` gets a compile error until they add `using Curl.Api.Codes;`.

Fix: add `using Curl.Api.Codes;` to the usings sample (preferred), or name the namespace where `CurlCode` is introduced. `Curl.Api.UnitTests/Readme/ReadmeTests.cs` compiles or checks the README's samples; keep it passing, and extend it if it does not catch a missing namespace for a type a sample uses.

## Acceptance criteria

- [ ] The `<!-- sample: usings -->` block in `Curl.Api/README.md` includes `using Curl.Api.Codes;`, or the README names `Curl.Api.Codes` where `CurlCode` is first used.
- [ ] `Curl.Api.UnitTests/Readme/ReadmeTests.cs` passes, and fails if `using Curl.Api.Codes;` is removed while a sample uses `CurlCode`, or the Notes say why that check is not possible.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from bug report 2026-11-18T1233-Curl-bug-report.md, finding 2.
