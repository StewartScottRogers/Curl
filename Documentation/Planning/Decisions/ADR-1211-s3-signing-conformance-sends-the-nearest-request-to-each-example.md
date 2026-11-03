# ADR-1211: S3 signing conformance sends the nearest request to each example

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4499
- Decided by Claude under Stewart's delegation.

## Context

BL-4499 asks the S3 signing cases to reproduce the Amazon S3 API reference's GET Object, PUT Object and listing
examples byte for byte through the curl command line. Those examples sign `Host: examplebucket.s3.amazonaws.com`
(no region), a `Range` header, `Date` and `x-amz-storage-class` headers, and a ListObjects (version 1) query. Curl's
`s3://` client sends `Host: bucket.s3.<region>.amazonaws.com` (ADR-1090), adds no headers from the command line,
and lists with ListObjectsV2, so no command line makes it send the examples' requests. That is the client's design,
not a signing defect: it signs whatever it sends.

## Decision

The published signatures are reproduced byte for byte by the documentation-written verifier
(`S3SigV4ReferenceAuthorizerTests`), which pins the verifier. The curl cases run at the examples' time
(2013-05-24 00:00:00 UTC) with the examples' access key and send the nearest request the command line makes - the
GET of `test.txt`, the PUT of `test$file.text` with the body `Welcome to Amazon S3.` (whose payload hash is the
example's), and the bucket listing - and assert the `Authorization` header byte for byte against the pinned
verifier's computation. `UNSIGNED-PAYLOAD`, presigned URLs and chunked signing are `n/a` in the matrix: the client
uses none of them.

## Consequences

A signing defect in the client fails a case through the independent verifier; the examples' own vectors guard the
verifier. Should the client ever send extra headers, a case can sign the example's exact request.
