---
id: BL-2201
title: Send no Basic Authorization from a redirect URL's credentials when --httpsig-* signs the request (upstream test5025)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-13
completed:
---
# BL-2201 — Send no Basic Authorization from a redirect URL's credentials when --httpsig-* signs the request (upstream test5025)

## Goal

Upstream curl 8.22.0's test5025 passes through Curl and is listed in `PassingUpstreamCases.txt`.

## Context

Found by BL-2183. Test5025 signs with `--httpsig-*` and `--location`; the first response redirects
to `http://bob:alice@example.org:9000/50250002`. curl 8.22.0 sends the hop with only the
`Signature-Input` and `Signature` lines: with httpsig on, the URL's credentials give no `Basic`
header (httpsig is the request's auth, as `--aws-sigv4` is). Curl sends
`Authorization: Basic Ym9iOmFsaWNl` on the hop (first difference at byte 367 of `<protocol>`, line
10, where `User-Agent:` is expected). Start at `Curl.Console/HttpSignatureHttpAuthenticator.cs` and
how a redirect hop's URL credentials reach the authenticator it wraps.

## Acceptance criteria

- [ ] A redirect hop whose URL carries credentials, under `--httpsig-*`, sends no `Authorization` line; a focused test in `Curl.Console.UnitTests` pins it (ADR-0482).
- [ ] `5025` is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` and its ratchet row passes.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
