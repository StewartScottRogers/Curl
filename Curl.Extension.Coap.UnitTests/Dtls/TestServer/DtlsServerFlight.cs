namespace Curl.Extension.Coap.Dtls.TestServer;

/// <summary>A group of records <see cref="DtlsTestServer" /> sends in answer to one client message.</summary>
internal enum DtlsServerFlight
{
    /// <summary>The HelloVerifyRequest: one record.</summary>
    HelloVerifyRequest,

    /// <summary>ServerHello, Certificate, ServerKeyExchange and ServerHelloDone: four records.</summary>
    ServerHello,

    /// <summary>ChangeCipherSpec and the protected Finished: two records.</summary>
    Finished,
}
