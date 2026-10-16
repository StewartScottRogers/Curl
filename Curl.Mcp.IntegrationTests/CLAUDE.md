# Curl.Mcp.IntegrationTests

The Integration tests of `Curl.Mcp` (ADR-0421): tests that touch something real outside the
process, such as a loopback socket that carries bytes. Every test carries
`[TestCategory("Integration")]`, so the fast test run skips them all. Namespace `Curl.Mcp`.

Today it holds `CurlTransferToolIntegrationTests`, the `transfer` tool through the host's real
composition (the production `ManagedCurlEngine`) against a loopback listener the test opens and
closes itself (BL-2384). BL-2390 adds the tests that start the real server over stdio.
