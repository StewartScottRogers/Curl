namespace Curl.Networking;

/// <summary>
/// The Windows certificate store locations curl's Schannel build accepts at the start of a
/// <c>--cert</c> store path, each valued as its <c>CERT_SYSTEM_STORE_*</c> flag, the number
/// curl prints when the store does not open.
/// </summary>
internal enum ClientCertificateStoreLocation
{
    /// <summary><c>CurrentUser</c>, <c>CERT_SYSTEM_STORE_CURRENT_USER</c>.</summary>
    CurrentUser = 0x10000,

    /// <summary><c>LocalMachine</c>, <c>CERT_SYSTEM_STORE_LOCAL_MACHINE</c>.</summary>
    LocalMachine = 0x20000,

    /// <summary><c>CurrentService</c>, <c>CERT_SYSTEM_STORE_CURRENT_SERVICE</c>.</summary>
    CurrentService = 0x40000,

    /// <summary><c>Services</c>, <c>CERT_SYSTEM_STORE_SERVICES</c>.</summary>
    Services = 0x50000,

    /// <summary><c>Users</c>, <c>CERT_SYSTEM_STORE_USERS</c>.</summary>
    Users = 0x60000,

    /// <summary><c>CurrentUserGroupPolicy</c>, <c>CERT_SYSTEM_STORE_CURRENT_USER_GROUP_POLICY</c>.</summary>
    CurrentUserGroupPolicy = 0x70000,

    /// <summary><c>LocalMachineGroupPolicy</c>, <c>CERT_SYSTEM_STORE_LOCAL_MACHINE_GROUP_POLICY</c>.</summary>
    LocalMachineGroupPolicy = 0x80000,

    /// <summary><c>LocalMachineEnterprise</c>, <c>CERT_SYSTEM_STORE_LOCAL_MACHINE_ENTERPRISE</c>.</summary>
    LocalMachineEnterprise = 0x90000,
}
