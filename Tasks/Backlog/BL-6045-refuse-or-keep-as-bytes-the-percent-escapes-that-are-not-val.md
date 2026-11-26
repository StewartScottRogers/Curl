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
completed:
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

- [ ] In each of the six libraries, a URL part holding `%E9` is refused naming the field (exit 3) or, where the field carries bytes, sent as the byte 0xE9; a unit test in that library's `.UnitTests` pins it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
