# Curl.Extension.Amqp1.UnitLibrary

The AMQP 1.0 client extension (ADR-0999, BL-4118). Namespace `Curl.Extension.Amqp1`. Tested against OASIS AMQP
Version 1.0 (29 October 2012), not against curl. Never references a `Curl.Protocol.*` library or another extension,
including `Curl.Extension.Amqp0.UnitLibrary` (AMQP 0-9-1, which owns `amqp://` and `amqps://`).

- `Types/`: the type system of Part 1 (ADR-1053). `Amqp1Writer` writes every value in its smallest encoding and
  `Amqp1Reader` reads every encoding; `Amqp1FormatCode` names the constructors of section 1.6. The value types are
  in `Amqp1Values.cs`: `Amqp1Symbol`, `Amqp1Timestamp`, `Amqp1Decimal`, `Amqp1Map`, `Amqp1Array` and
  `Amqp1Described` (section 1.3).
- Every fault in the encoding, read or about to be written, raises `Amqp1MalformedEncodingException`.
- `Frames/` (BL-4256, frames and performatives) and `Client/` (BL-4257, the client flow) are still to come. No option, handler or
  registration yet: BL-4118 adds them.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.Amqp1.UnitTests`.
