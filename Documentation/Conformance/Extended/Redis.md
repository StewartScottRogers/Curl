# Redis: RESP2, RESP3 and the redis URI scheme

Curl's `redis://` and `rediss://` client measured against the Redis serialization protocol specification
(<https://redis.io/docs/latest/develop/reference/protocol-spec/>: RESP2 and RESP3), the `HELLO`, `AUTH` and `SELECT`
command references, and the IANA provisional `redis` and `rediss` URI schemes. IDs are `Redis:<source>-<locator>`. The
cases run against `RedisReferenceServer` (`Curl.Conformance.Extended.UnitLibrary/Redis/`), which reads each command as
an array of bulk strings and sends the reply chunks the case scripts. Rendering follows `redis-cli --raw` (ADR-1079),
the library's own output choice, so the printed text is pinned but not a protocol requirement.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Redis:RESP-Request-1 | MUST | A client sends a command as an array of bulk strings. | RedisReplyTypeCases.SimpleString_ReplyOk_PrintsItsText |
| Redis:RESP-Request-2 | MUST | Bulk strings are binary safe: an argument holding NUL or CR LF is sent by its length. | RedisFramingCases.BinaryArguments_NulAndCrLfInPathAndData_AreSentAsBulkStringsByLength |
| Redis:RESP-Request-3 | MAY | A client sends inline commands (space-separated text). | n/a: an optional form for telnet-like clients; arrays of bulk strings are always accepted |
| Redis:RESP-Framing-1 | MUST | A reply is read by its type byte, CR LF lines and declared lengths, however the bytes arrive across reads. | RedisFramingCases.SplitReply_AcrossThreeReads_PrintsTheWholeBulkString |
| Redis:RESP-SimpleString-1 | MUST | `+` simple string. | RedisReplyTypeCases.SimpleString_ReplyOk_PrintsItsText |
| Redis:RESP-SimpleError-1 | MUST | `-` simple error is an error reply, not data. | RedisReplyTypeCases.SimpleError_Reply_FailsWithWeirdServerReplyAndItsText |
| Redis:RESP-Integer-1 | MUST | `:` signed 64-bit integer. | RedisReplyTypeCases.Integer_NegativeReply_PrintsItsDigits |
| Redis:RESP-BulkString-1 | MUST | `$` bulk string read by its length, CR LF inside included. | RedisReplyTypeCases.BulkString_HoldingCrLf_PrintsItsBytesByLength |
| Redis:RESP-Array-1 | MUST | `*` array of any types. | RedisReplyTypeCases.Array_OfTwoElements_PrintsEachElement |
| Redis:RESP-Aggregate-1 | MUST | Aggregates nest: arrays, maps and sets hold aggregates. | RedisFramingCases.NestedAggregates_ArrayHoldingArrayAndMapOfSet_PrintsEveryLeaf |
| Redis:RESP-Null-1 | MUST | `_` RESP3 null. | RedisReplyTypeCases.Null_Reply_PrintsAnEmptyLine |
| Redis:RESP2-Null-1 | MUST | `$-1` RESP2 null bulk string. | RedisHandshakeCases.Resp2_ServerRefusesHelloAndAnswersNullBulkString_GoesOnInResp2 |
| Redis:RESP2-Null-2 | MUST | `*-1` RESP2 null array. | RedisHandshakeCases.Resp2_ServerRefusesHelloAndAnswersNullArray_PrintsNothing |
| Redis:RESP-Boolean-1 | MUST | `#t` / `#f` boolean. | RedisReplyTypeCases.Boolean_False_PrintsFalse |
| Redis:RESP-Double-1 | MUST | `,` double, exponent form included. | RedisReplyTypeCases.Double_WithExponent_PrintsItsText |
| Redis:RESP-BigNumber-1 | MUST | `(` big number past 64 bits. | RedisReplyTypeCases.BigNumber_PastSixtyFourBits_PrintsEveryDigit |
| Redis:RESP-BulkError-1 | MUST | `!` bulk error is an error reply. | RedisReplyTypeCases.BulkError_Reply_FailsWithWeirdServerReplyAndItsText |
| Redis:RESP-VerbatimString-1 | MUST | `=` verbatim string: a three-byte format, a colon, then the text. | RedisReplyTypeCases.VerbatimString_Txt_PrintsTheTextAfterItsFormat |
| Redis:RESP-Map-1 | MUST | `%` map of key and value pairs. | RedisReplyTypeCases.Map_OfTwoEntries_PrintsEachKeyAndValue |
| Redis:RESP-Set-1 | MUST | `~` set. | RedisReplyTypeCases.Set_OfTwoMembers_PrintsEachMember |
| Redis:RESP-Attribute-1 | MUST | The attribute type (a pipe byte) is auxiliary data before a reply; a client that does not use it skips it and reads the reply. | RedisFramingCases.Attribute_BeforeTheReply_IsSkippedAndTheReplyPrinted |
| Redis:RESP-Push-1 | MUST | `>` push data is out of band and never taken for a command's reply. | RedisFramingCases.Push_BeforeTheReply_IsNotTakenForTheReply |
| Redis:HELLO-1 | MUST | `HELLO 3` switches the connection to RESP3 before other commands. | RedisReplyTypeCases.SimpleString_ReplyOk_PrintsItsText |
| Redis:HELLO-2 | SHOULD | A server that refuses `HELLO` speaks RESP2; the client goes on in RESP2. | RedisHandshakeCases.Resp2_ServerRefusesHelloAndAnswersNullBulkString_GoesOnInResp2 |
| Redis:HELLO-3 | MUST | `HELLO 3 AUTH <username> <password>` authenticates in the handshake. | RedisHandshakeCases.Auth_FromUserOption_IsSentInHello |
| Redis:HELLO-4 | MUST | A `NOAUTH` error to `HELLO` means the server needs authentication. | RedisHandshakeCases.Hello_ServerAnswersNoAuth_FailsWithLoginDenied |
| Redis:HELLO-5 | MAY | `HELLO ... SETNAME <clientname>` names the connection. | n/a: optional; curl names no connection |
| Redis:AUTH-1 | MUST | Authentication sends the user name and password. | RedisHandshakeCases.Auth_FromUserOption_IsSentInHello |
| Redis:AUTH-2 | MUST | With only a password the user is `default`. | RedisHandshakeCases.Auth_FromUrlPasswordWithoutUser_AuthenticatesAsDefault |
| Redis:AUTH-3 | MUST | A `WRONGPASS` error is a failed login. | RedisHandshakeCases.Auth_ServerAnswersWrongPass_FailsWithLoginDenied |
| Redis:SELECT-1 | MUST | `SELECT <index>` picks the logical database before the commands that use it. | RedisHandshakeCases.Select_DatabaseInPath_IsSelectedBeforeTheCommand |
| Redis:SELECT-2 | MUST | An error to `SELECT` leaves the database unselected; the command is not sent. | RedisHandshakeCases.Select_ServerAnswersOutOfRange_FailsBeforeTheCommand |
| Redis:URI-1 | MUST | A URI without a port connects to 6379. | RedisReplyTypeCases.SimpleString_ReplyOk_PrintsItsText |
| Redis:URI-2 | MUST | The user information `user:password` authenticates. | RedisHandshakeCases.Auth_FromUrlUserAndPassword_IsSentInHello |
| Redis:URI-3 | MUST | User information `:password` without a user authenticates with the password alone. | RedisHandshakeCases.Auth_FromUrlPasswordWithoutUser_AuthenticatesAsDefault |
| Redis:URI-4 | MUST | The path's first segment, a number, is the database. | RedisHandshakeCases.Select_DatabaseInPath_IsSelectedBeforeTheCommand |
| Redis:URI-5 | MUST | The authority's port is the one connected to. | RedisUriCases.Port_InTheAuthority_IsTheOneConnectedTo |
| Redis:URI-6 | MUST | `rediss` runs the same exchange over TLS. | RedisUriCases.Rediss_RunsTheSameExchangeOverTls |
| Redis:URI-7 | MAY | A `db` query parameter names the database. | n/a: the provisional registration's path form is the one Curl reads (ADR-1079) |

## Uploads

`-d` appends its bytes to the command as one more argument. `-T <file>` does the same with the whole file: `curl -T
up.txt redis://host/SET/kk` with `up.txt` holding `filebody` sends `SET kk filebody`, so an upload is a publish and
never turns the command into a fetch. When both are given, `-d`'s bytes are the argument. Pinned by
`RedisUploadTests` (`Curl.Extended.Protocol.Redis.UnitTests/Client/`), a library choice rather than a RESP requirement.
