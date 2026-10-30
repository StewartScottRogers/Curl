# Curl.Extension.Amqp0.UnitLibrary

The AMQP 0-9-1 client extension (ADR-0999), for `amqp://` and `amqps://` (BL-4117). Namespace `Curl.Extension.Amqp0`.
Tested against the AMQP 0-9-1 specification (2008-11-13) with RabbitMQ's errata and the class and method ids of
`amqp0-9-1.extended.xml`, not against curl (upstream curl has no AMQP). Never references `Curl.Extension.Amqp1.*`
(AMQP 1.0, BL-4118) or another extension.

- `Wire/`: the protocol header (section 4.2.2, `Amqp0ProtocolHeader`), frames (4.2.3, `Amqp0Frame`), the data types
  (4.2.5, `Amqp0Writer` and `Amqp0Reader`), field tables (4.2.5.5, `Amqp0FieldTable`, `Amqp0FieldValue`,
  `Amqp0FieldTableCodec`), the content header (4.2.6.1, `Amqp0ContentHeader` with `Amqp0BasicProperties`), body frames
  split at frame-max (`Amqp0Content`) and collected again (`Amqp0ContentAssembler`).
- `Methods/`: one record per method (connection, channel, queue.declare and the basic methods a publish and get
  need), each writing its own arguments; `Amqp0MethodReader` reads a method frame into its record.
- Every fault in the wire format, read or about to be written, raises `Amqp0WireFormatException` carrying the
  `Amqp0ReplyCode` (ADR-1056).
- `Client/`: `Amqp0ProtocolHandler` for `amqp://` and `amqps://` over the `IConnector` it is given, the URL's
  `/virtual-host/queue` (`Amqp0UrlQueue`), the publish or basic.get flow (`Amqp0ClientFlow`) over whole frames
  (`Amqp0FrameChannel`), and its exit codes and messages (ADR-1060), and `Amqp0Extension`, the description and handler factory
  `Curl.Extension.Composition` registers (ADR-1085).

References only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`). Held to the four
quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.Amqp0.UnitTests`.
