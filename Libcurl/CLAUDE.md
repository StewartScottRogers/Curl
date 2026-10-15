# Libcurl

The libcurl-faithful programming interface to Curl (ADR-0506): named `Libcurl`, with no `Curl.`
prefix, to match upstream's library name. A naming exception, with `Curl.Console` and `Curl.Api`.
Root namespace `Libcurl`. Empty of logic until phase 4 adds the engine-building overloads.

Reference `Curl.Api.UnitLibrary` only. Never reference `Curl.Api`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Libcurl.UnitTests`,
which sorts after every `Curl.*` project.
