# Curl.Protocol.Ldap.UnitLibrary

Phase 5.

Directory search. BER/DER encoded, unlike every other protocol here.

**URL schemes:** `ldap`, `ldaps`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. Take `IConnection`
so the tests in the matching `.UnitTests` project can drive this code from a
recorded byte stream with no network.

Diagnostic log (`--log-level`, ADR-0222, BL-929): `LdapTransferLog` writes under the
`ldap` component - the bind by DN, the search's base, scope and filter and how many entries
it returned (`info`), each message sent and search reply received by operation and
messageID (`verbose`) - and, for every transfer, its end: bytes and milliseconds at `info`,
or its `CurlExitCode` with the build's text for the LDAP result code at `error`. A bind's
password and tokens are never written.
