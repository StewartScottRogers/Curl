---
id: BL-6045
title: Refuse or keep as bytes the percent-escapes that are not valid UTF-8 in the remaining extension URL decoders (amqp1, s3, gcs, az, dns, mcp stdio)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Client, Curl.Extended.Protocol.S3.UnitLibrary/Client, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client, Curl.Extended.Protocol.DnsQuery.UnitLibrary/Client, Curl.Extended.Protocol.McpClient.UnitLibrary/Client, Curl.Extended.Protocol.Amqp1.UnitTests, Curl.Extended.Protocol.S3.UnitTests, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests, Curl.Extended.Protocol.AzureStorage.UnitTests, Curl.Extended.Protocol.DnsQuery.UnitTests, Curl.Extended.Protocol.McpClient.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6045 — Refuse or keep as bytes the percent-escapes that are not valid UTF-8 in the remaining extension URL decoders (amqp1, s3, gcs, az, dns, mcp stdio)

## Goal

A URL whose percent-escapes are not valid UTF-8 never makes these extensions act on the literal `%XX` text.

## Context

Follow-up to BL-6030 (fitness finding EXT-128, ADR-1429). `Curl.Extended.Protocol.{Amqp1,S3,GoogleCloudStorage,
AzureStorage,DnsQuery,McpClient}.UnitLibrary` still decode URL parts with `Uri.UnescapeDataString`, which leaves such
escapes undecoded. Not reproduced yet: confirm each by a unit test first. Use `UrlPercentDecoding` in
`Curl.Extension.Abstractions`.

## Acceptance criteria

- [x] In each of the six libraries, a URL part holding `%E9` is refused naming the field (exit 3) or, where the field carries bytes, sent as the byte 0xE9; a unit test in that library's `.UnitTests` pins it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Confirmed by unit tests; all six now refuse with exit 3 naming the field, before anything connects or starts (ADR-1447).
  Refusing rather than sending bytes: every one of these fields is text to its protocol or to this client (the dns name
  is presentation-format ASCII; `\DDD` already names any octet).
- Azure checks the whole path and query up front, because the Shared Key signer (outside touches) signs them decoded.
- dns `TYPE=`/`CLASS=` values with such an escape were already refused as unknown; a test pins it.
- Fast tests green; the six libraries are 100% line and branch, no member over the complexity or CRAP gate.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 11d35580 3d836d5e.
- 2026-11-26: Doing -> Done. amqp1, s3, gs, az, dns and mcp stdio refuse URL escapes that are not valid UTF-8 with exit 3 naming the part (ADR-1447)
