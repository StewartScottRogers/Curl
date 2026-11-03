# Grpc: server reflection and the proto3 JSON mapping

Curl's `grpc://` call made without `--grpc-proto`, its method described by gRPC server reflection, measured against
grpc/grpc's `doc/server-reflection.md` and `src/proto/grpc/reflection/v1/reflection.proto`, and its `-d` and output JSON
measured against protobuf's "ProtoJSON Format" (<https://protobuf.dev/programming-guides/json/>) (BL-4478). IDs are
`Grpc:Reflection-<topic>-<n>` and `Grpc:ProtoJson-<topic>-<n>`. The ProtoJSON guide states most of its mapping as what
"is" emitted and "is accepted", not with RFC 2119 words; this matrix reads each such rule as a MUST and keeps SHOULD
where the guide says "should". Part 1, the request and the unary call, is [Grpc.md](Grpc.md).

The cases are `GrpcReflectionCases`, `GrpcProtoJsonCases` and `GrpcProtoJsonWellKnownCases` in `Curl.Conformance.Extended.UnitTests/Grpc/Reflection/`.
They run against `GrpcReferenceServer` with `GrpcReflectionService` (`Curl.Conformance.Extended.UnitLibrary/Grpc/Reflection/`),
which serves serialized `FileDescriptorProto`s the cases write by hand. The reference server sends one reply per call,
so the reflection service answers the whole stream once the first request arrives: the requested file, then the
`file_by_filename` answers a client asks for next; the cases assert the requests curl actually sent from the recorded
call. The JSON cases send `-d` JSON to `GrpcEchoService`, which answers with the request's own bytes, and compare the
printed JSON with its whitespace removed.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Grpc:Reflection-Method-1 | MUST | Reflection is the bidirectional-streaming method `/grpc.reflection.v1.ServerReflection/ServerReflectionInfo`, called as a gRPC call carrying `ServerReflectionRequest` messages. | GrpcReflectionCases.Reflection_ServiceFoundBySymbol_DescribesTheMethodAndMakesTheCall |
| Grpc:Reflection-Symbol-1 | MUST | `file_containing_symbol` names the fully-qualified symbol, `<package>.<service>`, without a leading period. | GrpcReflectionCases.Reflection_ServiceFoundBySymbol_DescribesTheMethodAndMakesTheCall |
| Grpc:Reflection-Dependencies-1 | MUST | A dependency the server's files list but did not send is asked for by `file_by_filename`, by the name the `dependency` list gives it. | GrpcReflectionCases.Reflection_FileWithImports_FetchesEachImportByFileName |
| Grpc:Reflection-Dependencies-2 | MUST | A `file_descriptor_response` may carry several `FileDescriptorProto`s (the file and its dependencies); the client uses every one and asks for none of them again. | GrpcReflectionCases.Reflection_FileAndImportsInOneResponse_AsksForNoFileByName |
| Grpc:Reflection-Error-1 | MUST | An `error_response` answers the request with an error; the client reports its code and message and makes no call. | GrpcReflectionCases.Reflection_UnknownService_ReportsTheErrorResponseAndMakesNoCall |
| Grpc:Reflection-Unimplemented-1 | SHOULD | A server without the reflection service answers UNIMPLEMENTED; the client reports that the method cannot be described and makes no call. | GrpcReflectionCases.Reflection_ServerWithoutReflection_ReportsUnimplementedAndMakesNoCall |
| Grpc:Reflection-Version-1 | MAY | A client may fall back to `grpc.reflection.v1alpha.ServerReflection` when `v1` is UNIMPLEMENTED. | n/a: server-reflection.md requires no fallback, and Curl's client speaks v1 only (ADR-1149) |
| Grpc:Reflection-Server-1 | MUST | The server answers each request in the order received and may send the file's transitive dependencies with it. | n/a: binds the server, not the client |
| Grpc:ProtoJson-Names-1 | MUST | A field's JSON key is its lowerCamelCase name (or its `json_name`). | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Names-2 | MUST | The parser accepts both the lowerCamelCase name and the original proto field name. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Int64-1 | MUST | `int64`, `uint64`, `sint64`, `fixed64` and `sfixed64` are emitted as decimal strings. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Int64-2 | MUST | 64-bit integers are accepted as numbers or strings. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Int32-1 | MUST | `int32`, `uint32`, `sint32`, `fixed32` and `sfixed32` are emitted as JSON numbers. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Int32-2 | MUST | 32-bit integers are accepted as numbers or strings. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Float-1 | MUST | `float` and `double` are emitted as JSON numbers. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Float-2 | MUST | The special values are the strings `"NaN"`, `"Infinity"` and `"-Infinity"`, accepted and emitted. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Float-3 | MUST | Exponent notation is accepted. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Bool-1 | MUST | `bool` is `true` or `false`. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-String-1 | MUST | `string` is a JSON string. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Bytes-1 | MUST | `bytes` is emitted as standard base64 with padding. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Bytes-2 | MUST | Standard or URL-safe base64, with or without padding, is accepted. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Enum-1 | MUST | An enum is emitted as its value's name. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Enum-2 | MUST | An enum is accepted as its value's name or its integer value. | GrpcProtoJsonCases.Parse_AlternativeForms_EncodesTheSameValues |
| Grpc:ProtoJson-Message-1 | MUST | A message field is a JSON object. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Repeated-1 | MUST | A repeated field is a JSON array. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Map-1 | MUST | A map field is a JSON object whose keys are strings. | GrpcProtoJsonCases.RoundTrip_EveryScalarAndCompositeType_PrintsTheCanonicalJson |
| Grpc:ProtoJson-Default-1 | MUST | A field with its default value is omitted from the output. | GrpcProtoJsonCases.RoundTrip_DefaultsAndNulls_SendsAndPrintsNothing |
| Grpc:ProtoJson-Null-1 | MUST | `null` is accepted for every field type and means the default value. | GrpcProtoJsonCases.RoundTrip_DefaultsAndNulls_SendsAndPrintsNothing |
| Grpc:ProtoJson-Unknown-1 | SHOULD | The parser rejects unknown fields by default. | GrpcProtoJsonCases.Parse_UnknownField_IsRejectedBeforeTheCall |
| Grpc:ProtoJson-Timestamp-1 | MUST | `google.protobuf.Timestamp` is an RFC 3339 string, emitted Z-normalized. | GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms |
| Grpc:ProtoJson-Duration-1 | MUST | `google.protobuf.Duration` is a string of seconds ending in `s`, emitted with 0, 3, 6 or 9 fractional digits. | GrpcProtoJsonCases.RoundTrip_TimestampAndDuration_UseTheirStringForms |
| Grpc:ProtoJson-Any-1 | MUST | `google.protobuf.Any` holding an ordinary message is a JSON object with the type URL under `"@type"` beside the embedded message's own fields. | GrpcProtoJsonWellKnownCases.RoundTrip_AnyOfAMessage_IsTheTypeUrlBesideTheMessagesFields |
| Grpc:ProtoJson-Any-2 | MUST | An `Any` holding a well-known type with a special JSON form carries that form under `"value"` beside `"@type"`. | GrpcProtoJsonWellKnownCases.RoundTrip_AnyOfAWellKnownType_PutsItsJsonFormInValue |
| Grpc:ProtoJson-Struct-1 | MUST | `google.protobuf.Struct` is a JSON object whose members are its fields. | GrpcProtoJsonWellKnownCases.RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray |
| Grpc:ProtoJson-Struct-2 | MUST | `google.protobuf.Value` is any JSON value, `ListValue` a JSON array, and `NullValue` JSON `null`. | GrpcProtoJsonWellKnownCases.RoundTrip_StructValueAndListValue_AreAnyJsonObjectValueAndArray |
| Grpc:ProtoJson-Wrappers-1 | MUST | Each wrapper type (`DoubleValue` to `BytesValue`) is the JSON form of the value it wraps. | GrpcProtoJsonWellKnownCases.RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero |
| Grpc:ProtoJson-Wrappers-2 | MUST | A present wrapper is emitted even when it wraps its type's default value. | GrpcProtoJsonWellKnownCases.RoundTrip_WrapperTypes_AreTheWrappedValuesAndKeepAZero |
| Grpc:ProtoJson-FieldMask-1 | MUST | `google.protobuf.FieldMask` is one string of its paths in lowerCamelCase, joined by commas. | GrpcProtoJsonWellKnownCases.RoundTrip_FieldMask_IsOneStringOfLowerCamelCasePaths |
| Grpc:ProtoJson-Empty-1 | MUST | `google.protobuf.Empty` is an empty JSON object. | GrpcProtoJsonWellKnownCases.RoundTrip_Empty_IsAnEmptyObject |
