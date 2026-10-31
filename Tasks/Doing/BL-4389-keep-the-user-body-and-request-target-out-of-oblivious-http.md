---
id: BL-4389
title: Keep the user body and request target out of Oblivious HTTP key and relay requests
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Extension.ObliviousHttp.UnitLibrary/Relay/HookRequest.cs, Curl.Extension.ObliviousHttp.UnitTests/Relay]
requirement: none
created: 2026-10-31
completed:
---
# BL-4389 — Keep the user body and request target out of Oblivious HTTP key and relay requests

## Goal

The Oblivious HTTP extension's own key-configuration GET and relay POST never carry the user's -d body or --request-target, so nothing meant to be encapsulated reaches the relay or key server in clear.

## Context

Shift-end review 2026-10-31 (silent-failure-hunter, type-design-analyzer). Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs:16-26: WithRequest with a null body keeps context.PostData ("null keeps the transfer's own"), and neither WithRequest nor WithoutOwnHeaders (40-52) clears HttpRequestOptions.RequestTarget. Curl.Extension.ObliviousHttp.UnitLibrary/Relay/KeyConfigurationSource.cs and HookRequest.cs:33-37 use them, so `curl --ohttp-relay R --ohttp-keys http://k/ -d secret https://t/` sends a GET with body `secret` in clear to the key URL, and `--request-target /private/account` puts the hidden path on the relay's request line - defeating RFC 9458. Also an empty non-null body gives a GET a needless `Content-Type: application/x-www-form-urlencoded` (lines 21-24). High: it leaks user data the extension exists to hide.

## Acceptance criteria

- [ ] The key-configuration GET carries no body, no Content-Type and the key URL's own path even when -d and --request-target are given, proven by a test in Curl.Extension.ObliviousHttp.UnitTests/Relay.
- [ ] The relay POST's request line is the relay URL's path, never --request-target, proven by a test.
- [ ] TransferContextRequests has a test for its no-body case in Curl.Extension.Abstractions.UnitTests/Transfer.
- [ ] dotnet build is clean, the fast tests pass and both libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: 640168f4 9d92ce2b 4b5b2cba 9d71032c.
