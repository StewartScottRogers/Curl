# CoAP requirement matrix (RFC 7959 and RFC 7641)

Curl's `coap://` block-wise transfers (`Curl.Extended.Protocol.Coap.UnitLibrary`, `Blockwise/`) and `--coap-observe`
(`Observe/`) measured against RFC 7959 and RFC 7641 by the cases in
`Curl.Conformance.Extended.UnitTests/Coap/BlockwiseObserve/`, run against `CoapReferenceServer` with the block and
Observe options of `Curl.Conformance.Extended.UnitLibrary/Coap/BlockwiseObserve/`. This part (BL-4471) holds the
requirements of the two RFCs that bind a client; `Coap.md` holds RFC 7252's. Where the RFC states a rule a client
follows without a keyword, the Level says "(unkeyworded)". A row whose case fails is listed in
`Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with its fix task.

| ID | Level | Requirement | Covered by |
|----|-------|-------------|------------|
| Coap:RFC7959-2.2-1 | MUST NOT | The reserved SZX value 7 is not sent. | CoapBlock1Tests.Block1Upload_EachBlockFollowsA231Continue |
| Coap:RFC7959-2.3-1 | (unkeyworded) | A Block2 option in a request asks for the block NUM at size SZX; a request without it asks for block 0. | CoapBlock2Tests.Block2Download_IsFetchedBlockByBlockAndWrittenWhole |
| Coap:RFC7959-2.3-2 | MUST | A Block option that is not understood or is malformed in a response is treated as the response's error, not as a block. | n/a: a server sending a malformed Block2 is pinned by the library's own unit tests (exit 56); the reference server sends only well-formed block values |
| Coap:RFC7959-2.4-1 | MUST | In a Block2 request the M bit is 0. | CoapBlock2Tests.Block2Download_IsFetchedBlockByBlockAndWrittenWhole |
| Coap:RFC7959-2.4-2 | (unkeyworded) | A client takes the blocks of a response in order and joins them into the whole representation. | CoapBlock2Tests.Block2Download_IsFetchedBlockByBlockAndWrittenWhole |
| Coap:RFC7959-2.4-3 | SHOULD | Later Block2 requests use the size the server answered with, a smaller SZX the server chooses mid-transfer included. | CoapBlock2Tests.ServerLowersSzxMidTransfer_LaterBlocksAreAskedAtTheSmallerSize |
| Coap:RFC7959-2.4-4 | MUST NOT | Blocks whose ETag differs from the first block's are not joined into one representation. | CoapBlock2Tests.EtagChangesBetweenBlocks_TheMixedBodyIsNotWrittenAndExit56 |
| Coap:RFC7959-2.4-5 | MAY | A client asks for a block size early by sending Block2 with block 0 in its first request. | n/a: an optional early negotiation curl does not use; its first request carries no Block2 |
| Coap:RFC7959-2.5-1 | (unkeyworded) | A request payload larger than one block goes in Block1 blocks, M set on all but the last. | CoapBlock1Tests.Block1Upload_EachBlockFollowsA231Continue |
| Coap:RFC7959-2.5-2 | (unkeyworded) | The next Block1 block is sent only after a 2.31 Continue for the one before. | CoapBlock1Tests.Block1Upload_EachBlockFollowsA231Continue |
| Coap:RFC7959-2.5-3 | SHOULD | A smaller SZX in a 2.31's Block1 is used for the remaining blocks, at the matching NUM. | CoapBlock1Tests.ServerAsksForSmallerBlocksIn231_RemainingBlocksUseTheSmallerSize |
| Coap:RFC7959-2.9.1-1 | (unkeyworded) | Each block is its own confirmable exchange with a new Message ID; all keep the request's token. | CoapBlock1Tests.Block1Upload_EachBlockFollowsA231Continue |
| Coap:RFC7959-2.9.2-1 | (unkeyworded) | A 4.08 Request Entity Incomplete ends the upload as a final error response (exit 22 under `-f`). | CoapBlock1Tests.RequestEntityIncompleteOnAMissingBlock_EndsTheUploadAndFailsUnderF |
| Coap:RFC7959-2.9.3-1 | SHOULD | After a 4.13 Request Entity Too Large whose Block1 gives a smaller size, the payload is sent again from block 0 at that size. | CoapBlock1Tests.RequestEntityTooLargeWithSize1AndBlock1_PayloadIsSentAgainFromBlock0AtTheAskedSize |
| Coap:RFC7959-3.1-1 | (unkeyworded) | A GET whose response carries Block2 with M set is repeated with Block2 for the next block until a block without M. | CoapBlock2Tests.Block2Download_IsFetchedBlockByBlockAndWrittenWhole |
| Coap:RFC7959-3.4-1 | (unkeyworded) | The later blocks of a notification carrying Block2 are fetched by GETs without Observe, and the notification is taken whole. | CoapObserveTests.NotificationWithBlock2_LaterBlocksAreFetchedWithoutObserveUnderAnotherTokenAndWrittenWhole |
| Coap:RFC7959-4-1 | (unkeyworded) | A Size1 in a 4.13 is the size the server accepts; it does not stop the client restarting at the Block1 size given. | CoapBlock1Tests.RequestEntityTooLargeWithSize1AndBlock1_PayloadIsSentAgainFromBlock0AtTheAskedSize |
| Coap:RFC7959-4-2 | MAY | A client sends Size1 with its request payload, or Size2 0 to ask for the representation's size. | n/a: optional size indications curl does not send |
| Coap:RFC7641-3.1-1 | (unkeyworded) | A client registers with a GET carrying Observe 0 and a token. | CoapObserveTests.Registration_NotificationsAreWrittenInOrderAndAReorderedOneIsDropped |
| Coap:RFC7641-3.2-1 | (unkeyworded) | A response with Observe to the registration means the client is on the list of observers, and each notification is a response matched by token. | CoapObserveTests.Registration_NotificationsAreWrittenInOrderAndAReorderedOneIsDropped |
| Coap:RFC7641-3.2-2 | (unkeyworded) | A notification without Observe means the client is no longer an observer; the observation ends with it. | CoapObserveTests.Registration_NotificationsAreWrittenInOrderAndAReorderedOneIsDropped |
| Coap:RFC7641-3.2-3 | (unkeyworded) | A notification with an error code ends the observation. | CoapObserveTests.ConfirmableNotifications_EachIsAcknowledgedWithAnEmptyAcknowledgement |
| Coap:RFC7641-3.3.1-1 | SHOULD | A client that wants to stay an observer re-registers with the same token once Max-Age passes without a fresh notification. | CoapObserveTests.MaxAgePassesWithoutANotification_ReregistersAndTakesAnOlderSequenceAfter128Seconds |
| Coap:RFC7641-3.4-1 | MUST | A notification whose sequence number is not newer (V1 < V2 and V2 - V1 < 2^23, or V1 > V2 and V1 - V2 > 2^23) within 128 seconds is not used. | CoapObserveTests.Registration_NotificationsAreWrittenInOrderAndAReorderedOneIsDropped |
| Coap:RFC7641-3.4-2 | MUST | A notification arriving more than 128 seconds after the last one taken is newer whatever its sequence number. | CoapObserveTests.MaxAgePassesWithoutANotification_ReregistersAndTakesAnOlderSequenceAfter128Seconds |
| Coap:RFC7641-3.5-1 | MUST | A confirmable notification is acknowledged with an Empty Acknowledgement of its Message ID. | CoapObserveTests.ConfirmableNotifications_EachIsAcknowledgedWithAnEmptyAcknowledgement |
| Coap:RFC7641-3.5-2 | MAY | A client no longer interested rejects a confirmable notification with a Reset. | n/a: curl stays interested until the transfer ends and then deregisters (Coap:RFC7641-3.6-1); a message of another token is rejected under RFC 7252 (Coap.md) |
| Coap:RFC7641-3.6-1 | MAY | A client ending its interest sends a GET with Observe 1 and the registration's token (deregistration). | CoapObserveTests.TransferEndsByMaxTime_DeregistersWithObserve1AndTheRegistrationToken |
| Coap:RFC7641-4-1 | MUST, SHOULD | Server obligations: the observer list, notification sequence numbers, Max-Age and the confirmable notification rate. | n/a: binds the server, not the client |
