using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Usage",
    "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
    Justification = "Each route file registers its route from a module initializer so the route table needs no central switch and no reflection (Native AOT).",
    Scope = "namespaceanddescendants",
    Target = "~N:Curl.Conformance.Differential.Http")]
