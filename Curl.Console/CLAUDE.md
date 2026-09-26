# Curl.Console

Phase 1. The entry point, published native ahead-of-time as a single file so it
drops onto PATH as `curl.exe`.

This is the only project without a `.UnitLibrary` suffix, because it is an
executable rather than a library.

It is also the only project that references every library: composing the services
is its job. `CurlComposition` is the composition root, written as plain constructor
calls - no container, no reflection, no assembly scanning, so native AOT sees every
type. `Curl.Core.UnitLibrary` dispatches through the `IProtocolHandler` instances it is
given and must never reference a protocol library directly.

`Program.Main` only opens the standard streams, builds the composition and hands the
arguments to `CurlCommandRunner`, which parses them, runs each URL and prints curl's
`curl: (N) <message>` lines. `-o` files open on the first write through
`DeferredOutputFileStream`, which is how curl's exit 23 message comes out right.

A URL with no `-o` writes through `StandardOutputFailureDeferringStream`, which
models curl's 4096-byte stdio buffer: a failed standard output is reported as
`curl: Failed writing body` (exit 23) while the body fits the buffer, and as the
handler's own `(23)` write failure once it would not.
