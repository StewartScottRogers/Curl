# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`.
Empty of logic until phase 3 adds the engine-building overloads.

Reference `Curl.Api.UnitLibrary` only. Never reference `Libcurl`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.
