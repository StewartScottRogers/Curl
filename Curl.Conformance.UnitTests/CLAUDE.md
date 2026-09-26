# Curl.Conformance.UnitTests

The tests of `Curl.Conformance.UnitLibrary`. It follows ADR-0013
(`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`).

Today it holds `HarnessReferencesTests`, which pins that the harness library and
`curl` (`Curl.Console`, the runner each case will go through) are both copied beside
the tests, and `UpstreamTestCaseParserTests`, which drive the test-file parser from
inline test-file text, and `UpstreamTestFileExpanderTests`, which pin variable
substitution, `%if` evaluation and the inline instructions against upstream's `prepro`, and
`UpstreamTestDataTests`, which pin that the vendored `UpstreamTestData/` (curl 8.21.0's
`tests/data/test*` files and `COPYING`; see its `README.md`) is copied beside the tests. What it is to hold in full, per ADR-0013:

- the harness library's own tests;
- upstream test data vendored under `UpstreamTestData/` from one pinned curl release,
  with curl's `COPYING` notice beside it - tests never download anything;
- one data-driven method running every vendored case in `TestCategory("Conformance")`,
  part of the fast suite, as a ratchet: a case on the committed passing list must pass,
  and any other case is reported `Inconclusive` with its reason or first difference.

No sockets and no network, like every other `.UnitTests` project.
