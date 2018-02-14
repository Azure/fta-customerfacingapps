using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Relecloud.Web.Models;
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

        public static IHtmlContent LinkForSortType(this IHtmlHelper html, SearchRequest request, string sortOn, bool sortDescending, string linkText)
        {
            var routeValues = request.Clone();
            if (string.Equals(routeValues.SortOn, sortOn) && routeValues.SortDescending == sortDescending)
            {
                routeValues.SortOn = null;
                routeValues.SortDescending = false;
                linkText = "[X] " + linkText;
            }
            else
            {
                routeValues.SortOn = sortOn;
                routeValues.SortDescending = sortDescending;
            }
            return html.ActionLink(linkText, "Search", "Concert", null, null, null, routeValues, null);
        }

        public static IHtmlContent LinkForSearchFacet(this IHtmlHelper html, SearchRequest request, SearchFacet facet, SearchFacetValue facetValue)
        {
            var routeValues = request.Clone();
            var linkText = $"{facetValue.DisplayName} ({facetValue.Count})";
            if (string.Equals(facet.FieldName, nameof(Concert.Price), StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(routeValues.PriceRange))
                {
                    routeValues.PriceRange = null;
                    linkText = "[X] " + linkText;
                }
                else
                {
                    routeValues.PriceRange = facetValue.Value;
                }
            }
            else if (string.Equals(facet.FieldName, nameof(Concert.Genre), StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(routeValues.Genre))
                {
                    routeValues.Genre = null;
                    linkText = "[X] " + linkText;
                }
                else
                {
                    routeValues.Genre = facetValue.Value;
                }
            }
            else if (string.Equals(facet.FieldName, nameof(Concert.Location), StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(routeValues.Location))
                {
                    routeValues.Location = null;
                    linkText = "[X] " + linkText;
                }
                else
                {
                    routeValues.Location = facetValue.Value;
                }
            }
            return html.ActionLink(linkText, "Search", "Concert", null, null, null, routeValues, null);
        }
    }
}