---
id: BL-2836
title: Stand in for the GSS-API stub (CURL_STUB_GSS_CREDS) so upstream cases 2056, 2057 and 2094 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.GssStubStandIn.cs, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2836 — Stand in for the GSS-API stub (CURL_STUB_GSS_CREDS) so upstream cases 2056, 2057 and 2094 run

## Goal

Upstream cases 2056, 2057 and 2094 (Negotiate against upstream's stub GSS-API library) run in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or have a recorded reason.

## Context

The three cases name `GSS-API,Debug` and set `CURL_STUB_GSS_CREDS`: `"KRB5_Alice"` (2056, HTTP Negotiate authentication with stub krb5; 2094, SPNEGO with Kerberos still works when NTLM is blocked) and `"NTLM_Alice"` (2057, Negotiate blocked for stub NTLM credentials). Upstream's debug build links `tests/libtest/stub_gssapi.c` instead of a real GSS-API library: its fake `gss_init_sec_context` returns a token of the form `"KRB5_Alice":HTTP@127.0.0.1:1:AAAA...` which 2056's `<verify><protocol>` holds after `Authorization: Negotiate`. Read that file and `tests/data/test2056`, `test2057`, `test2094` at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0) for the exact token layout and which credentials the stub refuses.

Gap scorecard 2026-10-19_1329 has the three `excluded | debug-build-only`; on Windows they are skipped with `Curl lacks the feature Debug`. Curl implements Kerberos and SPNEGO by hand (root CLAUDE.md: "Decisions"), so the stand-in is a stub security provider the harness gives the Negotiate authenticator in place of the real one, the way ADR-0687 and ADR-0678 hand the engine a harness-only collaborator. Find the seam in `Curl.Authentication.UnitLibrary` (the Negotiate/SPNEGO security-context abstraction) first; if none exists, add the smallest injectable interface there, with its own tests. The stub belongs to the harness; neither `Libcurl` nor the curl tool reads `CURL_STUB_GSS_CREDS`.

Deferred task BL-693 (`--krb` for FTP) is unrelated: it is about FTP `AUTH GSSAPI`.

## Acceptance criteria

- [ ] `PlatformFor` reports `Debug` to a case that sets `CURL_STUB_GSS_CREDS` and to none that does not; a unit test pins both.
- [ ] A unit test pins the stub's token for `KRB5_Alice` against the bytes in `tests/data/test2056`'s `<protocol>`, and its refusal of `NTLM_Alice` as 2057 expects.
- [ ] 2056, 2057 and 2094 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
