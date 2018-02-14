using System.Collections.Generic;

namespace Relecloud.Web.Models
{
    public class SearchResponse<T>
    {
        public SearchRequest Request { get; set; }
        public ICollection<T> Results { get; set; }
        public ICollection<SearchFacet> Facets { get; set; }

        public SearchResponse(SearchRequest request, ICollection<T> results, ICollection<SearchFacet> facets)
        {
            this.Request = request;
            this.Results = results ?? new T[0];
            this.Facets = facets ?? new SearchFacet[0];
        }
    }
}