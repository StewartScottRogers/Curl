# Curl.Api

The .NET-idiomatic programming interface to Curl (ADR-0506), not a `Curl.<Area>.UnitLibrary`:
`Curl.Api` is a naming exception, with `Curl.Console` and `Libcurl`. Namespace `Curl.Api`
for every area folder (`Client/` holds `CurlClient` and `CurlClientOptions`; `Composition/`
holds the engine-building half of the partial `CurlClient`).

`CurlClient` (BL-2496) is constructed over an `ICurlEngine`, owns it unless told not to,
creates easy handles with `CurlClientOptions` applied (internal `CreateEasy`), and throws
`ObjectDisposedException` after `DisposeAsync`. Tests use the hand-written fakes in
`Curl.Api.UnitTests/Client`, never a real engine.

`CurlClient.Create()` and `Create(CurlClientOptions)` (BL-2497) build a client over a new
`ManagedCurlEngine`, constructed directly (no reflection), which the client owns and disposes.
`Curl.Api.UnitTests/Composition` is the one place tests use the real engine, for version info
and handle creation only; nothing is sent over the network.

Reference `Curl.Api.UnitLibrary` (the seam) and `Curl.Engine.UnitLibrary` (only for
`ManagedCurlEngine` in `Composition/`), nothing else. Never reference `Libcurl` or a protocol
library.

Held to the four quality gates in the root `CLAUDE.md`. Its tests live in `Curl.Api.UnitTests`.
