# Curl.Console

Phase 1. The entry point, published native ahead-of-time as a single file so it
drops onto PATH as `curl.exe`.

This is the only project without a `.UnitLibrary` suffix, because it is an
executable rather than a library.

It is also the only project that references every library: composing the
dependency-injection container is its job. `Curl.Core.UnitLibrary` dispatches
through injected `IProtocolHandler` instances and must never reference a protocol
library directly.
