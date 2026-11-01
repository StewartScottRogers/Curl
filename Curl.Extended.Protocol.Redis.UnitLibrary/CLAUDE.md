# Curl.Extended.Protocol.Redis.UnitLibrary

The Redis extension (ADR-0999, ADR-1079), tested against the RESP3 specification and the redis URI scheme (IANA),
not against curl. It references only `Curl.Extension.Abstractions` (and through it `Curl.Protocol.Abstractions`) and
the base class library. The handler is registered by one line in `ExtensionCatalogue.cs` and described in
`ExtensionDescriptionCatalogue.cs`.

`Client/` (namespace `Curl.Extended.Protocol.Redis.Client`):

- `RedisExtension` is the description `curl --extensions` lists and the handler factory, over
  `ProtocolHandlerServices.Connector`.
- `RedisProtocolHandler` serves `redis://` and `rediss://` (TLS), both on port 6379.
- `RedisUrlCommand` reads the URL path `/[db]/command/argument/...`; `-d` appends one more argument.
- `RedisClientFlow` runs `HELLO 3` (with `AUTH` from `--user` or the URL), `SELECT` when the path names a database,
  then the command, writing its reply. `RedisClientMessages` holds the error texts and `RedisClientException` carries
  the exit code.

`Wire/` (namespace `Curl.Extended.Protocol.Redis.Wire`):

- `RespReply` and `RespReplyKind` are one reply and its type.
- `RespCommandEncoding` writes a command as an array of bulk strings.
- `RespReplyReader` reads replies off an `IConnection`: every RESP3 type, RESP2's `-1` nulls, attributes dropped.
- `RespReplyRendering` writes a reply as `redis-cli --raw` does.
