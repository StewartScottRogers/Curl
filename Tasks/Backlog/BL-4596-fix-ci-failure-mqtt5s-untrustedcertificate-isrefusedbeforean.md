---
id: BL-4596
title: Fix CI failure Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4596 — Fix CI failure Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 on Linux and macOS

## Goal

`Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60` failed on Linux and macOS in CI run 41560119555 (https://github.com/StewartScottRogers/Curl/actions/runs/41560119555). First failing commit: bcc7d1f5.

    Test method Curl.Conformance.Extended.UnitTests.Mqtt5.Transports.TlsCases.Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60 threw exception:

Lanes test only on Windows, so reproduce with `gh run view 41560119555 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `Mqtt5s_UntrustedCertificate_IsRefusedBeforeAnyMqttWithExit60` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-02: Created.
