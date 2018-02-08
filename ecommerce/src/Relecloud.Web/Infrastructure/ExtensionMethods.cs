using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Security.Claims;

namespace Relecloud.Web.Infrastructure
{
    public static class ExtensionMethods
    {
        public static Uri CdnUrl { get; set; }

        public static string GetUniqueId(this ClaimsPrincipal user)
        {
            // Azure AD issues a globally unique user ID in the objectidentifier claim.
            return user?.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");
        }

        public static void Set<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonConvert.SerializeObject(value));
        }

        public static T Get<T>(this ISession session, string key)
        {
            var value = session.GetString(key);
            return value == null ? default(T) : JsonConvert.DeserializeObject<T>(value);
        }

        public static string CdnContent(this IUrlHelper url, string contentPath)
        {
            var path = url.Content(contentPath);
            if (CdnUrl != null)
            {
                return new Uri(CdnUrl, path).ToString();
            }
            return path;
        }
    }
}