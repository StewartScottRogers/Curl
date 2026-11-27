# ADR-1447: The remaining extension protocols refuse URL escapes that are not valid UTF-8

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6045)

## Context

ADR-1429 left the amqp1, s3, gcs, azure storage, dns and mcp stdio extensions decoding URL parts with
`Uri.UnescapeDataString`, which leaves an escape that is not valid UTF-8 undecoded. Where the part was
encoded again (an S3 key, an Azure list prefix) or used as it came out (an AMQP address, a DNS name, a
program argument), the extension acted on the literal `%XX` text.

## Decision

Each refuses such a part with exit 3, naming it, before anything is connected or started:

- amqp1: the address.
- s3: the object key and the query. gs: the object name and the query.
- az, abfs, abfss: the path and the query, including parts that would be sent as they are.
- dns: the DNS name. A `TYPE=` or `CLASS=` value with such an escape was already refused as unknown.
- mcp+stdio: the program and each argument.

The handlers decode with `UrlPercentDecoding` (ADR-1429), except for the dns `TYPE=` and `CLASS=` values. Azure Storage's
Shared Key string-to-sign still uses `Uri.UnescapeDataString`, on a query already checked.

## Why

All of these are text in their protocol or API: AMQP 1.0 addresses are strings, S3, Cloud Storage and
Azure Storage names are UTF-8, and a program argument is a .NET string. A DNS label can hold any octet, but
this client reads the name as presentation-format text that holds only ASCII, where `\DDD` (`%5C233`)
already names any octet. Azure Storage signs the decoded query as text, so a query that is passed through
unchanged would still be signed as the literal text; refusing the whole path and query is the one rule that
covers both.
