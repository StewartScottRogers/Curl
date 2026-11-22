---
id: BL-5758
title: Make every extension receive a Curl.Api request body exactly as the console's -d data, pinned by one test across the extension registry
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/RequestBodies, Curl.Api.UnitTests/Extensions/ExtensionBodyParityTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5758 — Make every extension receive a Curl.Api request body exactly as the console's -d data, pinned by one test across the extension registry

## Goal

For every extension scheme registered in `Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs`, a `Curl.Api` request body (bytes, string or stream, with any method that carries a body) reaches the extension exactly as the console's `-d` data does, and one test walks the whole registry so a newly added extension that differs fails the build's tests at once.

## Context

Stewart, 2026-11-22: "file the extension-body task". The cookbook (Surly) has now reported five times that a `Curl.Api` body does not reach an extension the way `-d` does, each fixed per scheme: kafka, grpc, signalr (BL-5611); amqp, amqp1, mqtt5 (BL-5681); mqtt5 again (BL-5745, after BL-5681); mcp, mcps, mcp+stdio (BL-5754); webtransport (BL-5755). IMAP APPEND (BL-5610, then BL-5753) is the original-protocol version of the same gap. Fixing scheme by scheme leaves every scheme nobody has tried yet (stomp, nats, redis, xmpp, socketio, coap, dns, oci, s3, gs, az, amqp1s, plugin helpers, ...) to fail the same way later, and two of the fixes did not hold.

The console's `-d` lands on the transfer context's post data (`ReadOnlyMemory<byte>? PostData` on `Curl.Extension.Abstractions.UnitLibrary/Transfer/*TransferContext.cs`), and extensions decide publish-or-receive and request-or-list from it. `Curl.Api` turns a body into `CURLOPT_POST` fields or, since BL-5594, an upload for `-T` schemes (`Curl.Api/RequestBodies/UploadSchemes.cs`), and some of those forms do not end up in `PostData`. Read the ADRs from BL-5594, BL-5611 and BL-5681 first.

**Do:**
1. One rule, in one place in `Curl.Api`: for a URL whose scheme an extension serves, a body becomes exactly what `-d` would set (the same transfer-context fields, the same method semantics), except where that extension takes uploads with `-T` and `UploadSchemes` says so; record it in an ADR that supersedes the per-scheme parts of the earlier ones.
2. A test (`Curl.Api.UnitTests/Extensions/ExtensionBodyParityTests.cs`) that enumerates every scheme in the extension catalogue and, for each, builds the transfer context the console builds for `-d <bytes>` and the one `Curl.Api` builds for a `POST` with the same bytes (and a `PUT` where the scheme takes `-T`), and asserts they carry the same body and method. A scheme added to the catalogue is covered without editing the test.
3. Remove any per-scheme special case the rule makes redundant, keeping the extensions' own behaviour unchanged.

## Acceptance criteria

- [ ] An ADR records the one rule for `Curl.Api` bodies on extension schemes.
- [ ] `ExtensionBodyParityTests` enumerates the extension catalogue (not a hand-kept list) and passes for every registered scheme; adding a dummy extension whose body path differs makes it fail (shown in the Notes).
- [ ] The cookbook's earlier failing shapes still pass: kafka produce, grpc call, signalr invoke, amqp/amqp1/mqtt5 publish, mcp tools/call, webtransport echo, IMAP APPEND (each pinned by its existing test or a new one).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created at Stewart's request after the fifth Surly report of a Curl.Api body not reaching an extension.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 28cb5b82 2b23dfc9 6033af1b 3d836d5e.
