using System;

namespace Relecloud.Web.Infrastructure
{
    public class AuthenticationConfiguration
    {
        #region Configuration Bound Properties

        public string ClientId { get; set; }
        public string Tenant { get; set; }
        public string SignUpSignInPolicyId { get; set; }
        public string ResetPasswordPolicyId { get; set; }
        public string EditProfilePolicyId { get; set; }

        #endregion

        #region Derived Properties

        public string DefaultPolicy => SignUpSignInPolicyId;
        public string TenantName => Tenant.Replace(".onmicrosoft.com", string.Empty, StringComparison.OrdinalIgnoreCase); // The raw AAD B2C tenant name without the domain suffix.
        public string Authority => $"https://{TenantName}.b2clogin.com/tfp/{Tenant}/{DefaultPolicy}/v2.0";
        // The deprecated "login.microsoftonline.com" URL used the following Authority:
        // public string Authority => $"https://login.microsoftonline.com/tfp/{Tenant}/{DefaultPolicy}/v2.0";

        #endregion
    }
}