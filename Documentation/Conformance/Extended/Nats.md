# NATS requirement matrix: the client protocol

Curl's `nats://` and `natss://` client (`Curl.Extended.Protocol.Nats.UnitLibrary`, ADR-1065) measured against the
NATS client protocol (docs.nats.io/reference/reference-protocols/nats-protocol) through the real curl command line
(BL-4487). The cases in `Curl.Conformance.Extended.UnitTests/Nats/` run curl against a `ScriptedReferenceServer`
whose bytes are written from the specification: the protocol is line text, so canned server lines are its
reference server, and no NATS code is shared with the client. The document has few RFC keywords, so each "must",
"should" and each field the client writes or relies on is a row; IDs are `Nats:NATS-<operation>-<n>`.
Server-only statements and features curl does not use are `n/a`.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Nats:NATS-PROTO-1 | MUST | Every control line, and every payload, ends with CRLF. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-PROTO-2 | MUST | A client reads a control line however the server's bytes are split across reads. | NatsConnectCases.Publish_InfoSplitAcrossReads_HonoursItsMaxPayload |
| Nats:NATS-PROTO-3 | SHOULD | Operation names are matched case-insensitively. | n/a: the server's case, not the client's; curl sends upper case and matches what it receives case-insensitively |
| Nats:NATS-INFO-1 | MUST | The server sends INFO first; the client reads it before sending CONNECT. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-INFO-2 | MUST NOT | A client does not publish a payload larger than INFO's `max_payload`. | NatsConnectCases.Publish_InfoSplitAcrossReads_HonoursItsMaxPayload |
| Nats:NATS-INFO-3 | MUST | When INFO says `tls_required`, the client upgrades to TLS before CONNECT. | NatsConnectCases.Publish_Natss_SpeaksTlsFromTheFirstByteAndSaysTlsRequired |
| Nats:NATS-INFO-4 | MUST | When INFO says `auth_required`, the client sends credentials in CONNECT; without them the server answers `-ERR`. | NatsConnectCases.Subscribe_AuthRequiredAndErrAtConnect_FailsWithLoginDenied |
| Nats:NATS-INFO-5 | MAY | A server sends INFO again later (cluster updates); the client accepts it. | NatsConnectCases.Publish_OkAndInfoBeforePong_AreAccepted |
| Nats:NATS-INFO-6 | MUST | When INFO carries a `nonce`, a client authenticating with an nkey signs it in `sig`. | n/a: curl authenticates by user and password or token only, never nkey or JWT |
| Nats:NATS-CONNECT-1 | REQUIRED | CONNECT carries `verbose`. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-CONNECT-2 | REQUIRED | CONNECT carries `pedantic`. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-CONNECT-3 | REQUIRED | CONNECT carries `tls_required`. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-CONNECT-4 | REQUIRED | CONNECT carries `lang` and `version`; `name` is optional. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-CONNECT-5 | MUST | Credentials go as `user` and `pass`, or as `auth_token`. | NatsConnectCases.Publish_UserAndPassword_SendsThemAsUserAndPass |
| Nats:NATS-CONNECT-6 | SHOULD | `protocol` is 1 for a client that handles asynchronous INFO. | NatsConnectCases.Publish_NoCredentials_SendsConnectWithEveryRequiredFieldAndNoAuthField |
| Nats:NATS-CONNECT-7 | MUST | `tls_required` in CONNECT is true when the client speaks TLS. | NatsConnectCases.Publish_Natss_SpeaksTlsFromTheFirstByteAndSaysTlsRequired |
| Nats:NATS-PUB-1 | MUST | PUB gives the payload's byte count, and the payload follows the line, ended by CRLF, CR LF inside it included. | NatsPublishCases.Publish_PayloadWithCrLf_IsFramedByItsByteCount |
| Nats:NATS-HPUB-1 | MUST | HPUB is sent only after CONNECT announced `headers`. | n/a: curl sends no HPUB |
| Nats:NATS-SUBJ-1 | MUST NOT | A PUB subject holds no wildcard (`*` or `>`). | NatsPublishCases.Publish_SubjectWithWildcard_IsRefusedBeforeConnecting |
| Nats:NATS-SUBJ-2 | MUST NOT | A subject holds no whitespace and no empty token. | NatsPublishCases.Publish_SubjectWithEmptyToken_IsRefusedBeforeConnecting |
| Nats:NATS-SUB-1 | MUST | SUB names the subject and a subscription ID unique on the connection. | NatsSubscribeCases.Subscribe_MsgPayloadsWithCrLf_AreWrittenWholeUntilTheServerCloses |
| Nats:NATS-SUB-2 | MAY | A SUB subject holds wildcards. | NatsSubscribeCases.Subscribe_WildcardSubject_IsAllowed |
| Nats:NATS-SUB-3 | MAY | SUB names a queue group. | n/a: curl subscribes without a queue group |
| Nats:NATS-UNSUB-1 | MUST | UNSUB with max_msgs ends the subscription after that many messages; the client stops reading there. | NatsSubscribeCases.Subscribe_CountTwo_SendsUnsubWithMaxMsgsAndStopsAfterTwo |
| Nats:NATS-MSG-1 | MUST | MSG gives subject, subscription ID, an optional reply-to and the byte count. | NatsSubscribeCases.Subscribe_MsgPayloadsWithCrLf_AreWrittenWholeUntilTheServerCloses |
| Nats:NATS-MSG-2 | MUST | A MSG payload is read by its byte count, CR LF inside it included, and is followed by CRLF. | NatsSubscribeCases.Subscribe_MsgPayloadsWithCrLf_AreWrittenWholeUntilTheServerCloses |
| Nats:NATS-HMSG-1 | MUST | HMSG reaches only a client whose CONNECT set `headers`; one that did not treats HMSG as a protocol error. | NatsSubscribeCases.Subscribe_HmsgWithoutHeadersInConnect_IsAWeirdServerReply |
| Nats:NATS-PING-1 | MUST | A client answers the server's PING with PONG, or the server closes the connection. | NatsSubscribeCases.Subscribe_ServerPing_IsAnsweredWithPong |
| Nats:NATS-PING-2 | SHOULD | A client sends PING after CONNECT and waits for PONG to know its earlier lines were processed. | NatsPublishCases.Publish_PayloadWithCrLf_IsFramedByItsByteCount |
| Nats:NATS-OK-1 | MUST | `+OK` (sent when `verbose` is on) is accepted and ignored. | NatsConnectCases.Publish_OkAndInfoBeforePong_AreAccepted |
| Nats:NATS-ERR-1 | MUST | `-ERR` at connect (such as 'Authorization Violation') ends the connection; the client reports it. | NatsConnectCases.Subscribe_AuthRequiredAndErrAtConnect_FailsWithLoginDenied |
| Nats:NATS-ERR-2 | MUST | `-ERR` after PUB (such as 'Permissions Violation') is reported to the user. | NatsPublishCases.Publish_ErrAfterPub_FailsWithTheServersText |
| Nats:NATS-REQ-1 | MUST | A request subscribes to a unique inbox, publishes with it as reply-to and reads the reply there. | NatsPublishCases.Request_ReplyOnItsInbox_IsWrittenWithoutANewline |

## What `-T` does

`-T file` publishes the file's bytes exactly as `-d` publishes its data: the server receives `PUB <subject> <length>`
and the file's bytes, never a SUB, so an upload never subscribes, fetches or consumes. With `?request` the file is
the request's payload. When both are given, `-d`'s data is published and the upload is ignored (BL-4951). Pinned by
`NatsUploadTests` in `Curl.Extended.Protocol.Nats.UnitTests`.
