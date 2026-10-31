namespace Curl.Extension.ObliviousHttp.BinaryHttp;

/// <summary>The example messages of RFC 9292 section 5, as the hexadecimal the RFC shows, one RFC line per string.</summary>
internal static class Rfc9292Examples
{
    /// <summary>Figure 8: the request of Figure 7 in the known-length encoding.</summary>
    internal const string KnownLengthRequest =
        "00034745540568747470730" + "00a2f6865" +
        "6c6c6f2e747874406c0a757365722d61" +
        "67656e74346375726c2f372e31362e33" +
        "206c69626375726c2f372e31362e3320" +
        "4f70656e53534c2f302e392e376c207a" +
        "6c69622f312e322e3304686f73740f77" +
        "77772e6578616d706c652e636f6d0f61" +
        "63636570742d6c616e67756167650665" +
        "6e2c206d690000";

    /// <summary>Figure 11: the response of Figure 10, with 102 and 103 informational responses, in the indeterminate-length encoding.</summary>
    internal const string IndeterminateLengthResponse =
        "0340660772756e6e696e670a22736c65" +
        "65702031352200406704" + "6c696e6b233c" +
        "2f7374796c652e6373733e3b2072656c" +
        "3d7072656c6f61643b2061733d737479" +
        "6c65046c696e6b243c2f736372697074" +
        "2e6a733e3b2072656c3d7072656c6f61" +
        "643b2061733d7363726970740040c804" +
        "646174651d4d6f6e2c203237204a756c" +
        "20323030392031323a32383a35332047" +
        "4d5406736572766572064170616368" + "65" +
        "0d6c6173742d6d6f6469666965641d57" +
        "65642c203232204a756c203230303920" +
        "31393a31353a353620474d5404657461" +
        "6714223334616133383" + "72d642d313536" +
        "3865623030220d6163636570742d7261" +
        "6e676573056279746573" + "0e636f6e7465" +
        "6e742d6c656e67746802353104766172" +
        "790f4163636570742d456e636f64696e" +
        "670c636f6e74656e742d747970650a74" +
        "6578742f706c61696e003348656c6c6f" +
        "20576f726c6421204d7920636f6e7465" +
        "6e7420696e636c756465732061207472" +
        "61696c696e672043524c462e0d0a0000";

    /// <summary>Figure 13: the chunked response of Figure 12 in the known-length encoding, with a trailer.</summary>
    internal const string KnownLengthResponse =
        "0140c8001d5468697320636f6e74656e" +
        "7420636f6e7461696e732043524c462e" +
        "0d0a0d07747261696c65720474657874";
}
