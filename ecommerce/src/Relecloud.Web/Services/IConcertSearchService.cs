using Relecloud.Web.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Relecloud.Web.Services
{
    public interface IConcertSearchService
    {
        void Initialize();
        Task<SearchResponse<ConcertSearchResult>> SearchAsync(SearchRequest request);
        Task<ICollection<string>> SuggestAsync(string query);
    }
}