# OCI registry conformance

Curl's `oci://` client (`Curl.Extended.Protocol.OciRegistry.UnitLibrary`) measured against the OCI Distribution
Specification v1.1.0 (github.com/opencontainers/distribution-spec/blob/v1.1.0/spec.md) pull and push workflows and
its error codes, the OCI Image Specification's media types and descriptor digests, and Docker's Token Authentication
Specification (BL-4488). Without `-T` curl pulls (`:tag` a manifest, `@digest` a blob); with `-T` it pushes the upload
as the manifest, or as the blob once its SHA-256 matches the URL's digest. Content discovery (tags, referrers) and
content management (deletion) are workflows the client does not use.

The reference registry is `Curl.Conformance.Extended.UnitLibrary/OciRegistry/OciReferenceRegistry`, one instance
serving the registry API, the token endpoint and blob storage; the cases are in
`Curl.Conformance.Extended.UnitTests/OciRegistry/`. IDs are `OciRegistry:DIST-<section>-<n>` (Distribution
Specification), `OciRegistry:IMAGE-<section>-<n>` (Image Specification) and `OciRegistry:TOKEN-<section>-<n>` (Token
Authentication Specification).

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| OciRegistry:DIST-PullManifests-1 | MUST | A manifest is pulled with `GET /v2/<name>/manifests/<reference>`, the reference a tag. | OciPullCases.ManifestPull_ByTag_GetsTheManifestWithTheMediaTypesItAccepts |
| OciRegistry:DIST-PullManifests-2 | MUST | The `<reference>` of a manifest pull may be the manifest's digest. | OciPullCases.ManifestPull_ByDigest_GetsTheManifestUnderItsDigest |
| OciRegistry:DIST-PullManifests-3 | SHOULD | The client sends an `Accept` header naming the manifest media types it supports. | OciPullCases.ManifestPull_ByTag_GetsTheManifestWithTheMediaTypesItAccepts |
| OciRegistry:IMAGE-MediaTypes-1 | MUST | An image index (`application/vnd.oci.image.index.v1+json`) is a manifest type the client accepts. | OciPullCases.IndexPull_ByTag_GetsTheImageIndex |
| OciRegistry:DIST-PullBlobs-1 | MUST | A blob is pulled with `GET /v2/<name>/blobs/<digest>`. | OciPullCases.BlobPull_FollowsTheRedirectToBlobStorage |
| OciRegistry:DIST-PullBlobs-2 | SHOULD | The client follows the 307 redirect a registry may answer a blob GET with to the blob's storage. | OciPullCases.BlobPull_FollowsTheRedirectToBlobStorage |
| OciRegistry:IMAGE-Descriptor-Digests-1 | SHOULD | Content fetched by digest is verified against the digest before it is consumed. | OciPullCases.BlobPull_WhoseContentDoesNotMatchItsDigest_IsRefused |
| OciRegistry:DIST-CheckBlobs-1 | MAY | A client may `HEAD /v2/<name>/blobs/<digest>` and skip the upload of a blob the registry has. | OciPushCases.BlobPush_OfABlobTheRegistryHas_UploadsNothing |
| OciRegistry:DIST-PushBlobs-1 | MUST | A monolithic upload is `POST /v2/<name>/blobs/uploads/`, then `PUT <location>?digest=<digest>` with the whole blob. | OciPushCases.BlobPush_ToAnAbsoluteLocation_PostsThenPutsTheWholeBlobWithItsDigest |
| OciRegistry:DIST-PushBlobs-2 | MUST | The PUT carries `Content-Length` and `Content-Type: application/octet-stream`. | OciPushCases.BlobPush_ToAnAbsoluteLocation_PostsThenPutsTheWholeBlobWithItsDigest |
| OciRegistry:DIST-PushBlobs-3 | MUST | The upload goes to the `Location` the POST's 202 names, absolute on another host included. | OciPushCases.BlobPush_ToAnAbsoluteLocation_PostsThenPutsTheWholeBlobWithItsDigest |
| OciRegistry:DIST-PushBlobs-4 | MUST | A relative `Location` is resolved against the registry. | OciPushCases.BlobPush_ToARelativeLocation_PutsOnTheRegistry |
| OciRegistry:DIST-PushBlobs-5 | MUST | A `Location` that carries a query keeps it, the `digest` parameter added to it. | OciPushCases.BlobPush_ToALocationWithAQuery_KeepsTheQueryAndAddsTheDigest |
| OciRegistry:DIST-PushBlobs-6 | MUST | The `digest` given is the uploaded content's; a blob whose content does not match is not pushed under it. | OciPushCases.BlobPush_WhoseContentDoesNotMatchTheUrlDigest_IsRefusedBeforeAnyRequest |
| OciRegistry:DIST-PushBlobsChunked-1 | MUST | A chunked upload sends `PATCH` with `Content-Range` and `Content-Length`, in order. | n/a: the client uploads monolithically (POST then PUT), which the specification allows |
| OciRegistry:DIST-PushBlobsSinglePost-1 | MAY | A blob may be uploaded with a single `POST ...?digest=<digest>`. | n/a: the client uses POST then PUT, the workflow every registry must support |
| OciRegistry:DIST-MountBlob-1 | MAY | A blob may be mounted from another repository with `?mount=<digest>&from=<name>`. | n/a: the client never mounts blobs |
| OciRegistry:DIST-PushManifests-1 | MUST | A manifest is pushed with `PUT /v2/<name>/manifests/<reference>` whose `Content-Type` is the manifest's media type. | OciPushCases.ManifestPush_PutsTheManifestWithItsMediaType |
| OciRegistry:DIST-PushManifests-2 | SHOULD | The blobs a manifest references are pushed before the manifest. | n/a: one URL is one transfer; the order of the transfers is the caller's |
| OciRegistry:DIST-PushManifests-3 | MUST | A client pushing a manifest with a `subject` handles the referrers fallback when no `OCI-Subject` header is answered. | n/a: the client keeps no referrers index; content discovery is a workflow it does not use |
| OciRegistry:DIST-ContentDiscovery-1 | MUST | Tags are listed with `GET /v2/<name>/tags/list` and referrers with `GET /v2/<name>/referrers/<digest>`. | n/a: the client lists neither tags nor referrers |
| OciRegistry:DIST-ContentManagement-1 | MUST | Manifests, tags and blobs are deleted with `DELETE`. | n/a: the client deletes nothing |
| OciRegistry:DIST-ErrorCodes-1 | MUST | An error body is `{"errors":[{"code","message","detail"}]}`, its code one of code-1 to code-14; the client reports the code and message. | OciErrorCases.ErrorBody_FailsWithItsStatusCodeAndMessage |
| OciRegistry:TOKEN-Flow-1 | MUST | On a 401 with `WWW-Authenticate: Bearer realm,service,scope` the client requests a token from the realm with the `service` and `scope` parameters. | OciTokenAuthCases.AnonymousPull_AsksTheRealmForATokenAndRetriesWithIt |
| OciRegistry:TOKEN-Flow-2 | MUST | The client retries the request with `Authorization: Bearer <token>`. | OciTokenAuthCases.AnonymousPull_AsksTheRealmForATokenAndRetriesWithIt |
| OciRegistry:TOKEN-Request-1 | MUST | Credentials the user gives are sent to the token endpoint as Basic authentication. | OciTokenAuthCases.PullWithUser_SendsTheCredentialsToTheTokenEndpointAsBasic |
| OciRegistry:TOKEN-Request-2 | MUST | A token endpoint that refuses the credentials fails the transfer as an authentication failure. | OciTokenAuthCases.PullWithWrongPassword_FailsAsLoginDenied |
| OciRegistry:TOKEN-Response-1 | MUST | The token response's `token` member holds the token. | OciTokenAuthCases.AnonymousPull_AsksTheRealmForATokenAndRetriesWithIt |
| OciRegistry:TOKEN-Response-2 | MUST | For OAuth 2.0 compatibility the token is also accepted as `access_token`. | OciTokenAuthCases.TokenResponseWithAccessToken_IsUsedAsTheBearerToken |
| OciRegistry:TOKEN-Response-3 | SHOULD | A client caches a token until its `expires_in` and may refresh it with `refresh_token`. | n/a: one transfer asks for at most one token and keeps none past it |
| OciRegistry:TOKEN-Scope-1 | MUST | A push asks for the `repository:<name>:push,pull` scope. | OciTokenAuthCases.ManifestPush_AsksForThePushAndPullScope |
