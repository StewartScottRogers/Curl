# Curl.Extension.Protocol.Amqp1.UnitLibrary

The AMQP 1.0 client extension (ADR-0999, BL-4118). Namespace `Curl.Extension.Protocol.Amqp1`. Tested against OASIS AMQP
Version 1.0 (29 October 2012), not against curl. Never references a `Curl.Protocol.*` library or another extension,
including `Curl.Extension.Protocol.Amqp0.UnitLibrary` (AMQP 0-9-1, which owns `amqp://` and `amqps://`).

- `Types/`: the type system of Part 1 (ADR-1053). `Amqp1Writer` writes every value in its smallest encoding and
  `Amqp1Reader` reads every encoding; `Amqp1FormatCode` names the constructors of section 1.6. The value types are
  in `Amqp1Values.cs`: `Amqp1Symbol`, `Amqp1Timestamp`, `Amqp1Decimal`, `Amqp1Map`, `Amqp1Array` and
  `Amqp1Described` (section 1.3).
- `Frames/`: the transport of Part 2, the message sections of Part 3 and the SASL frames of Part 5 (ADR-1058).
  `Amqp1ProtocolHeader` writes and checks the `AMQP 0 1.0.0` and `AMQP 3 1.0.0` headers; `Amqp1FrameCodec` the frame
  layout and `Amqp1Frame` one frame and its performative and payload. `Amqp1CompositeType` catalogues every
  performative, error, delivery state, source, target, header, properties and SASL frame body with its descriptor
  code and fields, and `Amqp1Composite` is one such value. `Amqp1MessageCodec` writes and reads message sections
  (`Amqp1Section`, `Amqp1SectionType`); `Amqp1SaslInitialResponse` the PLAIN and ANONYMOUS initial responses.
- Every fault in the encoding, a frame or a header, read or about to be written, raises
  `Amqp1MalformedEncodingException`.
- `Client/`: the send/receive client of `amqp1://` and `amqp1s://` (ADR-1062). `Amqp1ProtocolHandler` serves the
  schemes, `Amqp1ClientFlow` runs the exchange after `Amqp1SaslNegotiation`, and `Amqp1Extension` holds the
  description and handler factory registered in `Curl.Extension.Composition` (ADR-1088). No options of its own.

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.Protocol.Amqp1.UnitTests`.
