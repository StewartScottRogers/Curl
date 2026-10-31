using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Usage",
    "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
    Justification = "Each SASL mechanism file registers its mechanism from a module initializer so the mechanism list needs no central switch and no reflection (Native AOT).",
    Scope = "namespaceanddescendants",
    Target = "~N:Curl.Extension.Protocol.Xmpp.Client")]
