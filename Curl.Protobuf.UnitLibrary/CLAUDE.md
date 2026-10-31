# Curl.Protobuf.UnitLibrary

Hand-built Protocol Buffers for the gRPC, gRPC-Web and Connect extensions (ADR-0999). The
base class library has no Protocol Buffers codec and extensions may not reference one
another, so it is a shared Wire layer, as `Curl.Zstandard` is (ADR-1005).

Namespace `Curl.Protobuf.Wire` (folder `Wire/`): the wire format codec (BL-4164).

- `ProtobufWriter`: varints, ZigZag `sint32`/`sint64`, fixed32/64, length-delimited and
  packed repeated fields, and `WriteField`, which writes back a field read unchanged.
- `ProtobufReader`: every read is a `TryRead...` returning a `ProtobufDecodeError`;
  malformed input never throws. `TryReadField` reads any field, a group whole, into a
  `ProtobufField`, which is how unknown fields round-trip. Groups and embedded messages
  nest at most `MaximumDepth` (100) deep.
- `ProtobufZigZag`: the ZigZag mapping.

Namespace `Curl.Protobuf.DynamicMessages` (folder `DynamicMessages/`): messages of a type
known only at run time (BL-4169, ADR-1112).

- `DynamicTypeCatalogue`: resolves the named types of a set of `ProtoFileDescriptor`s into
  `DynamicMessageType`s and their `DynamicField`s; a map field's entries are messages.
- `DynamicMessage`: field values boxed as the type `DynamicValueKind` names, and the unknown
  fields read. `DynamicMessageEncoder` writes it; `DynamicMessageDecoder.TryDecode` reads it,
  returning a `ProtobufDecodeError` for malformed input.

`Descriptors/` (`.proto` parsing, BL-4165) and `Json/` (the JSON mapping, BL-4166) go
beside `Wire/`. Base class library only, no reflection.
