# ADR-0746 — A run-time feature mask makes a run behave as a build without proxy, IPv6 or IDN

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2984
- Decided by Claude under Stewart's delegation.

## Context

Upstream's test suite runs some cases only against a libcurl built without a feature: case 375
expects a build without proxy support (`CURL_DISABLE_PROXY`) to answer `-x wohoo
http://example.com/` with `curl: proxy support is disabled in this libcurl` and exit 4, and cases
1454, 959-961 and 445 need builds without IPv6 or IDN (BL-2856). Curl is one build with every
feature, so the conformance harness needs a switch it can set per case without changing the
shipped tool.

## Decision

- The mask is `Curl.Engine.EngineFeatureMask`, an internal `[Flags]` enum in
  `Curl.Engine.UnitLibrary` with `None`, `Proxy`, `Ipv6` and `Idn`. `TransferDispatch.FeatureMask`
  carries it to each transfer; `CurlComposition.CreateRunner` takes it as one optional parameter,
  `featureMask`, defaulting to `None`.
- Under `Proxy`, a transfer with a proxy set fails in `TransferPerformer.TrySelectProxyAndCredentials`,
  before anything else, with `CURLE_NOT_BUILT_IN` (exit 4) and no error text, as libcurl's setopt
  refuses `CURLOPT_PROXY` in such a build and curl 8.22.0's `src/tool_operate.c` marks the error
  synthetic. The option group writes `curl: proxy support is disabled in this libcurl` to standard
  error before the transfer, as the tool does. Nothing is sent.
- `Ipv6` and `Idn` are named here so BL-2982 and BL-2983 build on the same type; until they land
  those flags change nothing.
- Neither the shipped curl tool nor `Libcurl` sets the mask: both use `None`, so their behaviour
  is unchanged. Only the conformance harness (BL-2856) passes another value.

## Consequences

- Cases that upstream runs only on reduced builds can run in Curl's harness with the mask set
  for that case.
- The mask is internal and run-time only; there is no command-line option or environment
  variable that sets it.
