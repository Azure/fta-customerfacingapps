using System.Security.Claims;

namespace Relecloud.Web.Infrastructure
{
    public static class ExtensionMethods
    {
        public static string GetUniqueId(this ClaimsPrincipal user)
        {
            // Azure AD issues a globally unique user ID in the objectidentifier claim.
            return user?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        }
    }
}