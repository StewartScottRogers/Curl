namespace Curl.Ntlm;

/// <summary>The <c>AvId</c> of an AV_PAIR in a CHALLENGE message's target information (MS-NLMP section 2.2.2.1).</summary>
public enum NtlmAvId : ushort
{
    /// <summary><c>MsvAvEOL</c>: ends the list.</summary>
    EndOfList = 0,

    /// <summary><c>MsvAvNbComputerName</c>: the server's NetBIOS computer name, UTF-16LE.</summary>
    NetBiosComputerName = 1,

    /// <summary><c>MsvAvNbDomainName</c>: the server's NetBIOS domain name, UTF-16LE.</summary>
    NetBiosDomainName = 2,

    /// <summary><c>MsvAvDnsComputerName</c>: the server's DNS computer name, UTF-16LE.</summary>
    DnsComputerName = 3,

    /// <summary><c>MsvAvDnsDomainName</c>: the server's DNS domain name, UTF-16LE.</summary>
    DnsDomainName = 4,

    /// <summary><c>MsvAvDnsTreeName</c>: the server's DNS forest name, UTF-16LE.</summary>
    DnsTreeName = 5,

    /// <summary><c>MsvAvFlags</c>: a 32-bit flag field.</summary>
    Flags = 6,

    /// <summary><c>MsvAvTimestamp</c>: the server's time as a 64-bit FILETIME.</summary>
    Timestamp = 7,

    /// <summary><c>MsvAvSingleHost</c>: a Single_Host_Data structure.</summary>
    SingleHost = 8,

    /// <summary><c>MsvAvTargetName</c>: the client's service principal name, UTF-16LE.</summary>
    TargetName = 9,

    /// <summary><c>MsvAvChannelBindings</c>: the MD5 hash of the channel bindings.</summary>
    ChannelBindings = 10,
}
