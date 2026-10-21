---
id: BL-2836
title: Stand in for the GSS-API stub (CURL_STUB_GSS_CREDS) so upstream cases 2056, 2057 and 2094 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2910]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/UpstreamConformanceTests.GssStubStandIn.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.DebugStandIns.cs, Curl.Conformance.UnitTests/UpstreamConformanceTests.cs, Curl.Console/CurlComposition.cs, Curl.Console.UnitTests/CurlCompositionTests.cs, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2836 — Stand in for the GSS-API stub (CURL_STUB_GSS_CREDS) so upstream cases 2056, 2057 and 2094 run

## Goal

Upstream cases 2056, 2057 and 2094 (Negotiate against upstream's stub GSS-API library) run in the harness and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` or have a recorded reason.

## Context

The three cases name `GSS-API,Debug` and set `CURL_STUB_GSS_CREDS`: `"KRB5_Alice"` (2056, HTTP Negotiate authentication with stub krb5; 2094, SPNEGO with Kerberos still works when NTLM is blocked) and `"NTLM_Alice"` (2057, Negotiate blocked for stub NTLM credentials). Upstream's debug build links `tests/libtest/stub_gssapi.c` instead of a real GSS-API library: its fake `gss_init_sec_context` returns a token of the form `"KRB5_Alice":HTTP@127.0.0.1:1:AAAA...` which 2056's `<verify><protocol>` holds after `Authorization: Negotiate`. Read that file and `tests/data/test2056`, `test2057`, `test2094` at tag `curl-8_22_0` (https://github.com/curl/curl/tree/curl-8_22_0) for the exact token layout and which credentials the stub refuses.

Gap scorecard 2026-10-19_1329 has the three `excluded | debug-build-only`; on Windows they are skipped with `Curl lacks the feature Debug`. Curl implements Kerberos and SPNEGO by hand (root CLAUDE.md: "Decisions"), so the stand-in is a stub security provider the harness gives the Negotiate authenticator in place of the real one, the way ADR-0687 and ADR-0678 hand the engine a harness-only collaborator. Find the seam in `Curl.Authentication.UnitLibrary` (the Negotiate/SPNEGO security-context abstraction) first; if none exists, add the smallest injectable interface there, with its own tests. The stub belongs to the harness; neither `Libcurl` nor the curl tool reads `CURL_STUB_GSS_CREDS`.

Deferred task BL-693 (`--krb` for FTP) is unrelated: it is about FTP `AUTH GSSAPI`.

## Acceptance criteria

- [x] `PlatformFor` reports `Debug` to a case that sets `CURL_STUB_GSS_CREDS` and to none that does not; a unit test pins both.
- [x] A unit test pins the stub's token for `KRB5_Alice` against the bytes in `tests/data/test2056`'s `<protocol>`, and its refusal of `NTLM_Alice` as 2057 expects.
- [x] 2056, 2057 and 2094 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`, or the first difference is in Notes and a follow-up task is filed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

- 2026-10-21 (lane 5, first pass, no code written): the seam already exists. `NegotiateHttpAuthenticator` asks an
  `ISecurityContextFactory` (Curl.Protocol.Abstractions), and `RankedHttpAuthenticator` already sends `--negotiate`
  pre-emptively on the first request, so `Curl.Authentication.UnitLibrary` needs no change. What the work does need:
  1. `Curl.Console/CurlComposition.cs`: the dialing `CreateRunner(..., ITcpDialer, IDnsResolver, ...)` overload, which
     `RunCurlAsync` uses, takes no `ISecurityContextFactory`. It needs an optional `securityContexts` parameter, passed
     to `CreateDialingTransferDispatch` in place of `CreateDialingSecurityContextFactory(...)` when given (plus a test
     in `Curl.Console.UnitTests/CurlCompositionTests.cs`). **BL-2910 (in Doing) touches this file**, so the task goes
     back to Backlog until BL-2910 is done.
  2. `UpstreamConformanceTests.GssStubStandIn.cs` (new): a harness-only `ISecurityContextFactory` that reads the
     case's `CURL_STUB_GSS_CREDS`, ported from upstream's `lib/curl_gssapi.c` at curl-8_22_0 (`stub_gss_init_sec_context`;
     note it lives in lib/, not tests/libtest/). First token = `<creds>:<service>@<host>:<sent>:` padded with `A` to
     187 bytes (250*3/4), `sent` 1 for KRB5 and 2 for NTLM1. The creds value keeps its quotes (`"KRB5_Alice"`), as the
     2056 `<protocol>` shows. SPNEGO's credential allows krb5 only, so creds without `KRB5` (2057's `"NTLM_Alice"`) fail
     with no mechanism: no header, exit 0. A `D` server token completes; `C` continues; anything else fails.
  3. `UpstreamConformanceTests.DebugStandIns.cs`: one rule line, `text.Contains("\nCURL_STUB_GSS_CREDS=")`, so
     `PlatformFor` reports Debug. 2056/2094 also need `GSS-API`. Check `Platform` reports it; if not, add it with `WithFeatures`.
  4. `UpstreamConformanceTests.cs`: `RunCurlAsync` passes `securityContexts:` the stub factory when the case's
     environment sets `CURL_STUB_GSS_CREDS`, else null.
- Touches widened for this (no Doing task named them except CurlComposition.cs): DebugStandIns.cs and
  UpstreamConformanceTests.cs (the rule table and RunCurlAsync), CurlComposition.cs and CurlCompositionTests.cs.
- 2026-10-21 (lane 3, done): did steps 1-4 above. `CurlComposition`'s dialing `CreateRunner` takes
  `securityContexts` (null keeps `CreateDialingSecurityContextFactory`), pinned by
  `CurlCompositionTests.CreateRunner_DialingWithGivenSecurityContexts_NegotiatesOnThem`. The stub lives in
  `UpstreamConformanceTests.GssStubStandIn.cs` with its tests; `GSS-API` was already reported on every platform.
  2056, 2057 and 2094 each passed with `CURL_UPSTREAM_CASE` on Windows and are on the list. The tokens are plain
  ASCII and nothing reads the disk or an OS error, so the cases do not depend on the platform. Measure-CodeQuality
  `-Library Curl.Console`: 100% line, 100% branch, max complexity 10.
- Choice (default taken): the stub models only what the three cases reach. First step: a krb5 token when the
  credentials contain `KRB5`, otherwise `NoMechanism`. A later server token `D` completes the context, and any
  other token is `Refused`. The upstream stub's `C` (continue) round and its NTLM tokens are not ported, because
  SPNEGO blocks NTLM and no vendored case sends `C`. It is harness-only, so no ADR.
- The last criterion (CI green on Linux and macOS) is checked after the shift pushes: the shift's coordinator
  watches CI and files a task for any failure (BL-987). Lanes do not push.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: f2c0ce47 cb803305 519beea4 dd1cc079 620ebead.
- 2026-10-21: Doing -> Backlog. Needs Curl.Console/CurlComposition.cs (a securityContexts parameter on the dialing CreateRunner), which BL-2910 in Doing touches; depends on BL-2910
- 2026-10-21: Backlog -> Doing. Criteria at claim: f2c0ce47 cb803305 519beea4 dd1cc079 620ebead.
- 2026-10-21: Doing -> Done. Stub GSS-API stand-in added; 2056, 2057 and 2094 pass and are on the ratchet
