---
id: BL-4993
title: Print Schannel's less-secure-TLS warning in verbose output when --ciphers is an ALG_ID list
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/SchannelTrustText.cs, Curl.Output.UnitTests/SchannelTrustTextTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4993 — Print Schannel's less-secure-TLS warning in verbose output when --ciphers is an ALG_ID list

## Goal

`curl -v --ciphers CALG_AES_128 https://...` in the Schannel build prints curl's `* schannel: WARNING:` line, as curl 8.22.0 does.

## Context

Measured in BL-4992 (ADR-1250): curl 8.22.0 Schannel prints, right after `schannel: disabled automatic use of client certificate`, the verbose line `* schannel: WARNING: This version of Schannel negotiates a less-secure TLS version than TLS 1.3 because the user set an algorithm cipher list.` when `--ciphers` is an ALG_ID list. Curl prints none. Start at `Curl.Output.UnitLibrary/SchannelTrustText.cs`; measure with `Record-CurlExchange.ps1 -Tls`.

## Acceptance criteria

- [ ] With the Schannel build and a `--ciphers` ALG_ID list, `-v` stderr carries the WARNING line at curl's position, pinned by a unit test; no line without such a list.

## Notes

## Log

- 2026-11-11: Created.
