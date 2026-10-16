# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`
for every area folder (`Client/` holds `CurlClient` and `CurlClientOptions`).

`CurlClient` (BL-2496) is constructed over an `ICurlEngine`, owns it unless told not to,
creates easy handles with `CurlClientOptions` applied (internal `CreateEasy`), and throws
`ObjectDisposedException` after `DisposeAsync`. Tests use the hand-written fakes in
`Curl.Api.UnitTests/Client`, never a real engine.

Reference `Curl.Api.UnitLibrary` only. Never reference `Libcurl`, a protocol library, or
`Curl.Engine.UnitLibrary` before the task that adds the engine-building overloads.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.
