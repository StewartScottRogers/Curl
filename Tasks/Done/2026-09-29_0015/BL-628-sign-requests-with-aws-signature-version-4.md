---
id: BL-628
title: Sign requests with AWS Signature Version 4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0178-aws-signature-version-4-signs-as-curls-http-aws-sigv4-c-quirks-included.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-628 — Sign requests with AWS Signature Version 4

## Goal

An `AwsSigV4Signer` in `Curl.Authentication.UnitLibrary` builds the canonical request, string to sign, signing key and `Authorization` header (plus `X-Amz-Date` and, where curl adds it, `x-amz-content-sha256`) exactly as curl 8.21.0 does for a given `--aws-sigv4 "provider1[:provider2[:region[:service]]]"` value, key pair, method, URL, headers, body hash and time.

## Context

- Conformance audit 2026-09-28, row 22 (Major, M). Wiring is BL-629.
- The algorithm: AWS's published Signature Version 4 documentation and its test suite; curl's variations (provider names other than `aws` change the header prefixes and algorithm name, how region and service are taken from the host when not given) are in `CurlManual.txt` (`--aws-sigv4`) and must be confirmed by measurement.
- `HMACSHA256` and `SHA256` from the BCL; inject the time.
- Measure with `Record-CurlExchange.ps1`: `--aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET` for a GET with a query, a POST with `-d`, and `--aws-sigv4 osc` with the host naming region and service; record `request.bin` (the date is in the request, so the test can recompute with it).

## Acceptance criteria

- [x] Measured first as above; request bytes copied into Notes.
- [x] `Curl.Authentication.UnitTests` reproduce each measured `Authorization` header byte for byte from the measured date, and pass AWS's published example vectors.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Decision recorded in ADR-0178 (decided by Claude under Stewart's delegation). The ADR and
  its README index row were added to `touches`: no task in Doing names either file.
- Measured with curl 8.21.0 (mingw, Schannel) via `Record-CurlExchange.ps1 -Port 18628`,
  `-u AKID:SECRET`; the algorithm read from curl 8.21.0's `lib/http_aws_sigv4.c` and every
  signature-changing edge measured. Request bytes (CRLF line ends):

  `--aws-sigv4 aws:amz:us-east-1:s3 http://127.0.0.1:18628/bucket/key%20a?b=2&a=1&c`
  ```
  GET /bucket/key%20a?b=2&a=1&c HTTP/1.1
  Host: 127.0.0.1:18628
  Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=6f96d5ca68a6971972090dfc3d89b3f9674419c900e06d56ef1c7b28373389d7
  X-Amz-Date: 20260929T060111Z
  x-amz-content-sha256: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
  User-Agent: curl/8.21.0
  Accept: */*
  ```
  `--aws-sigv4 aws:amz:us-east-1:s3 -d hello=world http://127.0.0.1:18628/upload`
  ```
  POST /upload HTTP/1.1
  Host: 127.0.0.1:18628
  Authorization: AWS4-HMAC-SHA256 Credential=AKID/20260929/us-east-1/s3/aws4_request, SignedHeaders=host;x-amz-content-sha256;x-amz-date, Signature=445ec533470e073a3ca813c59a7f730706cf61288fda6024ac5ff39fd37d85e7
  X-Amz-Date: 20260929T060111Z
  x-amz-content-sha256: 3d011e09502a84552a0f8ae112d024cc2c115597e3a577d5f49007902c221dc5
  User-Agent: curl/8.21.0
  Accept: */*
  Content-Length: 11
  Content-Type: application/x-www-form-urlencoded

  hello=world
  ```
  `--aws-sigv4 osc --connect-to ::127.0.0.1:18628 http://fcu.eu-west-2.outscale.com:18628/path`
  ```
  GET /path HTTP/1.1
  Host: fcu.eu-west-2.outscale.com:18628
  Authorization: OSC4-HMAC-SHA256 Credential=AKID/20260929/eu-west-2/fcu/osc4_request, SignedHeaders=host;x-osc-date, Signature=235bcff21c7d26e2cff783594087d84a221296654fe89689144b13fc6690bee5
  X-Osc-Date: 20260929T060112Z
  User-Agent: curl/8.21.0
  Accept: */*
  ```
  Also measured and pinned in `AwsSigV4SignerTests` (signature and date in each test): a `-T`
  upload to S3 with `AWS:Amz` (header `x-Amz-content-sha256: UNSIGNED-PAYLOAD`), custom
  headers trimmed and merged, `Empty;` signed and `Gone:` not; a custom `X-Amz-Date` and a user
  name escaped as `AK%20ID`; `aws:amz` on `127.0.0.1` (service `127`, region `0`); a custom
  `Date` that is no timestamp (empty date in the scope); the query quirks one by one; and the
  errors: `localhost` exit 3 service missing, `svc.localhost` exit 3 region missing, `:amz`
  exit 43, `-H "X-Amz-Date;"` exit 27 `Out of memory`, 128 query components exit 100
  `HTTP request too large` (127 sign), `--path-as-is` exit 43.
- Learned: an empty query key (`=z`) is signed as `(nil)=z` - curl's key buffer is never
  allocated and its printf writes a null `%s` as `(nil)`. Found when the first upload case's
  signature did not match; confirmed by measuring `=z&m` and `z=1&=b&=a` alone.
- Choice: equal-keyed query components keep their original order (stable sort); the
  measured `z=1&=b&=a` agrees.
- AWS vectors checked: get-vanilla, get-vanilla-query-order-key-case, post-vanilla, and the
  IAM `ListUsers` example with `Content-Type`.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. AwsSigV4Signer signs requests byte for byte as curl 8.21.0's --aws-sigv4, matching 17 measured signatures and AWS's published examples
