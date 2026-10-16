using Curl.Api.Codes;
using Libcurl.Misc.ErrorText;

namespace Libcurl.UnitTests.Misc.ErrorText;

[TestClass]
public sealed class CurlErrorTextTests
{
    [TestMethod]
    [DataRow(CurlMultiCode.CallMultiPerform, "Please call curl_multi_perform() soon")]
    [DataRow(CurlMultiCode.Ok, "No error")]
    [DataRow(CurlMultiCode.BadHandle, "Invalid multi handle")]
    [DataRow(CurlMultiCode.BadEasyHandle, "Invalid easy handle")]
    [DataRow(CurlMultiCode.OutOfMemory, "Out of memory")]
    [DataRow(CurlMultiCode.InternalError, "Internal error")]
    [DataRow(CurlMultiCode.BadSocket, "Invalid socket argument")]
    [DataRow(CurlMultiCode.UnknownOption, "Unknown option")]
    [DataRow(CurlMultiCode.AddedAlready, "The easy handle is already added to a multi handle")]
    [DataRow(CurlMultiCode.RecursiveApiCall, "API function called from within callback")]
    [DataRow(CurlMultiCode.WakeupFailure, "Wakeup is unavailable or failed")]
    [DataRow(CurlMultiCode.BadFunctionArgument, "A libcurl function was given a bad argument")]
    [DataRow(CurlMultiCode.AbortedByCallback, "Operation was aborted by an application callback")]
    [DataRow(CurlMultiCode.UnrecoverablePoll, "Unrecoverable error in select/poll")]
    [DataRow((CurlMultiCode)13, "Unknown error")]
    [DataRow((CurlMultiCode)(-2), "Unknown error")]
    public void Multi_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlMultiCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Multi(code));
    }

    [TestMethod]
    [DataRow(CurlShareCode.Ok, "No error")]
    [DataRow(CurlShareCode.BadOption, "Unknown share option")]
    [DataRow(CurlShareCode.InUse, "Share currently in use")]
    [DataRow(CurlShareCode.Invalid, "Invalid share handle")]
    [DataRow(CurlShareCode.NoMem, "Out of memory")]
    [DataRow(CurlShareCode.NotBuiltIn, "Feature not enabled in this library")]
    [DataRow((CurlShareCode)6, "CURLSHcode unknown")]
    [DataRow((CurlShareCode)(-1), "CURLSHcode unknown")]
    public void Share_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlShareCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Share(code));
    }

    [TestMethod]
    [DataRow(CurlUrlCode.Ok, "No error")]
    [DataRow(CurlUrlCode.BadHandle, "An invalid CURLU pointer was passed as argument")]
    [DataRow(CurlUrlCode.BadPartPointer, "An invalid 'part' argument was passed as argument")]
    [DataRow(CurlUrlCode.MalformedInput, "Malformed input to a URL function")]
    [DataRow(CurlUrlCode.BadPortNumber, "Port number was not a decimal number between 0 and 65535")]
    [DataRow(CurlUrlCode.UnsupportedScheme, "Unsupported URL scheme")]
    [DataRow(CurlUrlCode.UrlDecode, "URL decode error, most likely because of rubbish in the input")]
    [DataRow(CurlUrlCode.OutOfMemory, "A memory function failed")]
    [DataRow(CurlUrlCode.UserNotAllowed, "Credentials was passed in the URL when prohibited")]
    [DataRow(CurlUrlCode.UnknownPart, "An unknown part ID was passed to a URL API function")]
    [DataRow(CurlUrlCode.NoScheme, "No scheme present")]
    [DataRow(CurlUrlCode.NoUser, "No user present")]
    [DataRow(CurlUrlCode.NoPassword, "No password present")]
    [DataRow(CurlUrlCode.NoOptions, "No options present")]
    [DataRow(CurlUrlCode.NoHost, "No host present")]
    [DataRow(CurlUrlCode.NoPort, "No port number present")]
    [DataRow(CurlUrlCode.NoQuery, "No query present")]
    [DataRow(CurlUrlCode.NoFragment, "No fragment present")]
    [DataRow(CurlUrlCode.NoZoneId, "No zoneid present")]
    [DataRow(CurlUrlCode.BadFileUrl, "Bad file:// URL")]
    [DataRow(CurlUrlCode.BadFragment, "Bad fragment")]
    [DataRow(CurlUrlCode.BadHostname, "Bad hostname")]
    [DataRow(CurlUrlCode.BadIPv6, "Bad IPv6 address")]
    [DataRow(CurlUrlCode.BadLogin, "Bad login")]
    [DataRow(CurlUrlCode.BadPassword, "Bad password")]
    [DataRow(CurlUrlCode.BadPath, "Bad path")]
    [DataRow(CurlUrlCode.BadQuery, "Bad query")]
    [DataRow(CurlUrlCode.BadScheme, "Bad scheme")]
    [DataRow(CurlUrlCode.BadSlashes, "Unsupported number of slashes following scheme")]
    [DataRow(CurlUrlCode.BadUser, "Bad user")]
    [DataRow(CurlUrlCode.LacksIdn, "libcurl lacks IDN support")]
    [DataRow(CurlUrlCode.TooLarge, "A value or data field is larger than allowed")]
    [DataRow(CurlUrlCode.Backslash, "Found a backslash where a forward slash was expected")]
    [DataRow((CurlUrlCode)33, "CURLUcode unknown")]
    [DataRow((CurlUrlCode)(-1), "CURLUcode unknown")]
    public void Url_DefinedOrUnknownCode_AnswersUpstreamsTextOrFallback(CurlUrlCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Url(code));
    }

    [TestMethod]
    [DataRow(CurlHeaderCode.Ok, "No error")]
    [DataRow(CurlHeaderCode.BadIndex, "Header exists but not with this index")]
    [DataRow(CurlHeaderCode.Missing, "No such header exists")]
    [DataRow(CurlHeaderCode.NoHeaders, "No headers at all exist (yet)")]
    [DataRow(CurlHeaderCode.NoRequest, "No request with this number was used")]
    [DataRow(CurlHeaderCode.OutOfMemory, "Out of memory while processing")]
    [DataRow(CurlHeaderCode.BadArgument, "A requested argument was bad")]
    [DataRow(CurlHeaderCode.NotBuiltIn, "The header API was disabled in the build")]
    [DataRow((CurlHeaderCode)8, "CURLHcode unknown")]
    [DataRow((CurlHeaderCode)(-1), "CURLHcode unknown")]
    public void Header_DefinedOrUnknownCode_AnswersTheCurlheCommentOrFallback(CurlHeaderCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Header(code));
    }

    [TestMethod]
    public void AllFour_EveryDefinedCode_AnswersTextOtherThanTheFallback()
    {
        foreach (var code in Enum.GetValues<CurlMultiCode>())
        {
            Assert.AreNotEqual("Unknown error", CurlErrorText.Multi(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlShareCode>())
        {
            Assert.AreNotEqual("CURLSHcode unknown", CurlErrorText.Share(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlUrlCode>())
        {
            Assert.AreNotEqual("CURLUcode unknown", CurlErrorText.Url(code), code.ToString());
        }

        foreach (var code in Enum.GetValues<CurlHeaderCode>())
        {
            Assert.AreNotEqual("CURLHcode unknown", CurlErrorText.Header(code), code.ToString());
        }
    }

    // Every CURLE_* text of the table that moved here from Curl.Console (BL-2450), as it was there,
    // and the fallback for the CURLE_OBSOLETE* numbers the table does not name.
    [TestMethod]
    [DataRow(CurlCode.Ok, "No error")]
    [DataRow(CurlCode.UnsupportedProtocol, "Unsupported protocol")]
    [DataRow(CurlCode.FailedInit, "Failed initialization")]
    [DataRow(CurlCode.UrlMalformat, "URL using bad/illegal format or missing URL")]
    [DataRow(CurlCode.NotBuiltIn, "A requested feature, protocol or option was not found built-in in this libcurl due to a build-time decision.")]
    [DataRow(CurlCode.CouldntResolveProxy, "Could not resolve proxy name")]
    [DataRow(CurlCode.CouldntResolveHost, "Could not resolve hostname")]
    [DataRow(CurlCode.CouldntConnect, "Could not connect to server")]
    [DataRow(CurlCode.WeirdServerReply, "Weird server reply")]
    [DataRow(CurlCode.RemoteAccessDenied, "Access denied to remote resource")]
    [DataRow(CurlCode.FtpAcceptFailed, "FTP: The server failed to connect to data port")]
    [DataRow(CurlCode.FtpWeirdPassReply, "FTP: unknown PASS reply")]
    [DataRow(CurlCode.FtpAcceptTimeout, "FTP: Accepting server connect has timed out")]
    [DataRow(CurlCode.FtpWeirdPasvReply, "FTP: unknown PASV reply")]
    [DataRow(CurlCode.FtpWeird227Format, "FTP: unknown 227 response format")]
    [DataRow(CurlCode.FtpCantGetHost, "FTP: cannot figure out the host in the PASV response")]
    [DataRow(CurlCode.Http2, "Error in the HTTP2 framing layer")]
    [DataRow(CurlCode.FtpCouldntSetType, "FTP: could not set file type")]
    [DataRow(CurlCode.PartialFile, "Transferred a partial file")]
    [DataRow(CurlCode.FtpCouldntRetrFile, "FTP: could not retrieve (RETR failed) the specified file")]
    [DataRow(CurlCode.QuoteError, "Quote command returned error")]
    [DataRow(CurlCode.HttpReturnedError, "HTTP response code said error")]
    [DataRow(CurlCode.WriteError, "Failed writing received data to disk/application")]
    [DataRow(CurlCode.UploadFailed, "Upload failed (at start/before it took off)")]
    [DataRow(CurlCode.ReadError, "Failed to open/read local data from file/application")]
    [DataRow(CurlCode.OutOfMemory, "Out of memory")]
    [DataRow(CurlCode.OperationTimedOut, "Timeout was reached")]
    [DataRow(CurlCode.FtpPortFailed, "FTP: command PORT failed")]
    [DataRow(CurlCode.FtpCouldntUseRest, "FTP: command REST failed")]
    [DataRow(CurlCode.RangeError, "Requested range was not delivered by the server")]
    [DataRow(CurlCode.SslConnectError, "SSL connect error")]
    [DataRow(CurlCode.BadDownloadResume, "Could not resume download")]
    [DataRow(CurlCode.FileCouldntReadFile, "Could not read a file:// file")]
    [DataRow(CurlCode.LdapCannotBind, "LDAP: cannot bind")]
    [DataRow(CurlCode.LdapSearchFailed, "LDAP: search failed")]
    [DataRow(CurlCode.AbortedByCallback, "Operation was aborted by an application callback")]
    [DataRow(CurlCode.BadFunctionArgument, "A libcurl function was given a bad argument")]
    [DataRow(CurlCode.InterfaceFailed, "Failed binding local connection end")]
    [DataRow(CurlCode.TooManyRedirects, "Number of redirects hit maximum amount")]
    [DataRow(CurlCode.UnknownOption, "An unknown option was passed in to libcurl")]
    [DataRow(CurlCode.SetoptOptionSyntax, "Malformed option provided in a setopt")]
    [DataRow(CurlCode.GotNothing, "Server returned nothing (no headers, no data)")]
    [DataRow(CurlCode.SslEngineNotFound, "SSL crypto engine not found")]
    [DataRow(CurlCode.SslEngineSetFailed, "Can not set SSL crypto engine as default")]
    [DataRow(CurlCode.SendError, "Failed sending data to the peer")]
    [DataRow(CurlCode.RecvError, "Failure when receiving data from the peer")]
    [DataRow(CurlCode.SslCertProblem, "Problem with the local SSL certificate")]
    [DataRow(CurlCode.SslCipher, "Could not use specified SSL cipher")]
    [DataRow(CurlCode.PeerFailedVerification, "SSL peer certificate or SSH remote key was not OK")]
    [DataRow(CurlCode.BadContentEncoding, "Unrecognized or bad HTTP Content or Transfer-Encoding")]
    [DataRow(CurlCode.FilesizeExceeded, "Maximum file size exceeded")]
    [DataRow(CurlCode.UseSslFailed, "Requested SSL level failed")]
    [DataRow(CurlCode.SendFailRewind, "Send failed since rewinding of the data stream failed")]
    [DataRow(CurlCode.SslEngineInitFailed, "Failed to initialize SSL crypto engine")]
    [DataRow(CurlCode.LoginDenied, "Login denied")]
    [DataRow(CurlCode.TftpNotFound, "TFTP: File Not Found")]
    [DataRow(CurlCode.TftpPerm, "TFTP: Access Violation")]
    [DataRow(CurlCode.RemoteDiskFull, "Disk full or allocation exceeded")]
    [DataRow(CurlCode.TftpIllegal, "TFTP: Illegal operation")]
    [DataRow(CurlCode.TftpUnknownId, "TFTP: Unknown transfer ID")]
    [DataRow(CurlCode.RemoteFileExists, "Remote file already exists")]
    [DataRow(CurlCode.TftpNoSuchUser, "TFTP: No such user")]
    [DataRow(CurlCode.SslCacertBadfile, "Problem with the SSL CA cert (path? access rights?)")]
    [DataRow(CurlCode.RemoteFileNotFound, "Remote file not found")]
    [DataRow(CurlCode.Ssh, "Error in the SSH layer")]
    [DataRow(CurlCode.SslShutdownFailed, "Failed to shut down the SSL connection")]
    [DataRow(CurlCode.Again, "Socket not ready for send/recv")]
    [DataRow(CurlCode.SslCrlBadfile, "Failed to load CRL file (path? access rights?, format?)")]
    [DataRow(CurlCode.SslIssuerError, "Issuer check against peer certificate failed")]
    [DataRow(CurlCode.FtpPretFailed, "FTP: The server did not accept the PRET command.")]
    [DataRow(CurlCode.RtspCseqError, "RTSP CSeq mismatch or invalid CSeq")]
    [DataRow(CurlCode.RtspSessionError, "RTSP session error")]
    [DataRow(CurlCode.FtpBadFileList, "Unable to parse FTP file list")]
    [DataRow(CurlCode.ChunkFailed, "Chunk callback failed")]
    [DataRow(CurlCode.NoConnectionAvailable, "The max connection limit is reached")]
    [DataRow(CurlCode.SslPinnedPubKeyNotMatch, "SSL public key does not match pinned public key")]
    [DataRow(CurlCode.SslInvalidCertStatus, "SSL server certificate status verification FAILED")]
    [DataRow(CurlCode.Http2Stream, "Stream error in the HTTP/2 framing layer")]
    [DataRow(CurlCode.RecursiveApiCall, "API function called from within callback")]
    [DataRow(CurlCode.AuthError, "An authentication function returned an error")]
    [DataRow(CurlCode.Http3, "HTTP/3 error")]
    [DataRow(CurlCode.QuicConnectError, "QUIC connection error")]
    [DataRow(CurlCode.Proxy, "proxy handshake error")]
    [DataRow(CurlCode.SslClientCert, "SSL Client Certificate required")]
    [DataRow(CurlCode.UnrecoverablePoll, "Unrecoverable error in select/poll")]
    [DataRow(CurlCode.TooLarge, "A value or data field grew larger than allowed")]
    [DataRow(CurlCode.EchRequired, "ECH attempted but failed")]
    [DataRow(CurlCode.Obsolete20, "Unknown error")]
    [DataRow((CurlCode)1000, "Unknown error")]
    public void Easy_DefinedOrUnknownCode_AnswersLibcurlsTextOrFallback(CurlCode code, string expected)
    {
        Assert.AreEqual(expected, CurlErrorText.Easy(code));
    }
}