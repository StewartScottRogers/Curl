# ADR-1303: A null byte[] assigned to a Curl.Api request Body means no body (BL-5527)

Status: Accepted. Date: 2026-11-18. Decided by Claude under Stewart's delegation.

## Context

`CurlContent`'s implicit conversion from `byte[]` threw `ArgumentNullException` for null, yet `CurlRequest.Body` is `CurlContent?`, so `Body = bytes` with a `byte[]?` compiled without a warning (a user-defined conversion's parameter is not flow-checked against the argument's null state) and threw at run time.

## Decision

The operator takes `byte[]?` and returns `CurlContent?`, annotated `[return: NotNullIfNotNull(nameof(bytes))]`: null means no body, which `Body` already expresses by being null. A non-null array is sent unchanged.

## Consequences

The repro sends the request with no body. Assigning a null array to a non-nullable `CurlContent` now warns, as it should. The ReadOnlyMemory conversion is unchanged.
