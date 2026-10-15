# Curl.Api.UnitLibrary

The shared core of Curl's two programming interfaces (ADR-0506): the types both `Curl.Api`
(the .NET-idiomatic API) and `Libcurl` (the libcurl-faithful API) are built from, such as the
transfer options and result types. Namespace `Curl.Api`. Empty of logic until phase 2 fills it.

Reference `Curl.Protocol.Abstractions.UnitLibrary` only (for `CurlExitCode`). Never reference a
protocol library, `Curl.Engine.UnitLibrary`, `Curl.Api` or `Libcurl`.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`,
which tests both this library and `Curl.Api`.
