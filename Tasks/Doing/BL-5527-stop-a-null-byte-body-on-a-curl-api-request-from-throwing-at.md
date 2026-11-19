---
id: BL-5527
title: Stop a null byte[] Body on a Curl.Api request from throwing at run time without a compile-time warning
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/RequestBodies/CurlContent.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5527 — Stop a null byte[] Body on a Curl.Api request from throwing at run time without a compile-time warning

## Goal

Assigning a null `byte[]` to a `Curl.Api` request's `Body` either means "no body" or is flagged at compile time; it no longer compiles silently and then throws `ArgumentNullException` at run time.

## Context

From bug report `2026-11-18T1734-Curl-bug-report.md` (new finding 2, Low), found while writing Part 2 of the *Surly* cookbook. Build tested: `f4b30d981f`, Windows 11, .NET SDK 11.0.100. The repro came from the chapter writer and was not re-run for the report, so reproduce it first.

**Repro:**

```csharp
byte[]? bytes = upload ? data : null;
var request = new CurlRequest(HttpMethod.Put, url) { Body = bytes };   // ArgumentNullException
```

**Observed:** `ArgumentNullException` from the implicit conversion from `byte[]` to the body type. The report says the code compiles without a nullable warning.

**Expected:** either no body when the value is null, or a compile-time signal.

Where to start: `Curl.Api/RequestBodies/CurlContent.cs:75`, `public static implicit operator CurlContent(byte[] bytes)`, which calls `ArgumentNullException.ThrowIfNull(bytes)`. A `ReadOnlyMemory<byte>` conversion sits beside it at line 70. First find out why a `byte[]?` argument draws no nullable warning (the `ReadOnlyMemory<byte>` overload, `Body`'s declared type, or the warning level in the consuming project). Then choose between:

- **Null means no body:** a conversion taking `byte[]?` and returning `CurlContent?`, annotated `[return: NotNullIfNotNull(nameof(bytes))]`, if `Body` is nullable and a null `Body` already means no body.
- **Compile-time error:** an annotation or overload change so that assigning a `byte[]?` warns (an error under warnings-as-errors).

Record the choice and why in an ADR (decided by Claude under Stewart's delegation). Keep `Curl.Api` at the four quality gates.

## Acceptance criteria

- [ ] The repro either builds and sends the request with no body (no exception), or draws a nullable warning at compile time; the ADR names which, and why.
- [ ] Focused unit tests in `Curl.Api.UnitTests` pin the chosen behaviour, including a non-null `byte[]` body still being sent unchanged.
- [ ] `CurlContent`'s XML doc comments, and `Curl.Api/README.md` if it shows `Body` assignment, describe what a null `byte[]` does.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from bug report 2026-11-18T1734-Curl-bug-report.md, new finding 2.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 7195c7b4 85164bf3 4d64fe9f 3d836d5e.
