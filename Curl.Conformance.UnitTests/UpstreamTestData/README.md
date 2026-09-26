# Upstream test data

Every `test*` file from `tests/data` in curl at release tag **`curl-8_21_0`** (2013 files),
byte for byte, with curl's `COPYING` notice from the same tag beside them. They are the
upstream test cases the conformance harness runs (ADR-0013, decision 3: vendored and
pinned; tests never download anything).

- Source: https://github.com/curl/curl/tree/curl-8_21_0/tests/data
- Licence: the curl licence, in `COPYING` here (https://curl.se/docs/copyright.html).
- `.gitattributes` marks the files `-text`, so git keeps their line endings exactly.
- `Curl.Conformance.UnitTests.csproj` copies the `test*` files and `COPYING` to the test
  output directory; `UpstreamTestDataTests` asserts the count.

Do not edit these files by hand.

## Refresh to another tag

```
powershell -NoProfile -ExecutionPolicy Bypass -File Curl.Conformance.UnitTests\UpstreamTestData\Update-UpstreamTestData.ps1 -Tag curl-8_21_0
```

The script downloads the tag's source archive from GitHub, replaces every `test*` file in
this folder and `COPYING` wholesale, and prints the new count. After a refresh, update the
tag and count in this README and `VendoredTestFileCount` in `UpstreamTestDataTests.cs`.
Run with the pinned tag, it reproduces the folder with an empty `git status`.
