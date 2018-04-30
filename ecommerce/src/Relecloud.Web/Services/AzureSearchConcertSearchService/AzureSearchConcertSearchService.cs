using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Relecloud.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Relecloud.Web.Services.AzureSearchService
{
    public class AzureSearchConcertSearchService : IConcertSearchService
    {
        #region Constants

        private const string IndexNameConcerts = "concerts";
        private const int PriceFacetInterval = 20;

        #endregion

        #region Fields

        private readonly string searchServiceName;
        private readonly SearchCredentials searchServiceCredentials;
        private readonly string concertsSqlDatabaseConnectionString;
        private readonly SearchIndexClient concertsIndexClient;

        #endregion

        #region Constructors

        public AzureSearchConcertSearchService(string searchServiceName, string adminKey, string concertsSqlDatabaseConnectionString)
        {
            this.searchServiceName = searchServiceName;
            this.searchServiceCredentials = new SearchCredentials(adminKey);
            this.concertsSqlDatabaseConnectionString = concertsSqlDatabaseConnectionString;
            this.concertsIndexClient = new SearchIndexClient(this.searchServiceName, IndexNameConcerts, this.searchServiceCredentials);
        }

        #endregion

        #region Initialize

        public void Initialize()
        {
            var serviceClient = new SearchServiceClient(this.searchServiceName, this.searchServiceCredentials);
            InitializeConcertsIndex(serviceClient);
        }

        private void InitializeConcertsIndex(SearchServiceClient serviceClient)
        {
            // Create the index that will contain the searchable data from the concerts.
            var concertsIndex = new Index
            {
                Name = IndexNameConcerts,
                Fields = new[]
                {
                    new Field(nameof(Concert.Id), DataType.String) { IsKey = true, IsSearchable = false },
                    new Field(nameof(Concert.Artist), DataType.String, AnalyzerName.EnMicrosoft) { IsSearchable = true, IsRetrievable = true },
                    new Field(nameof(Concert.Genre), DataType.String, AnalyzerName.EnMicrosoft) { IsSearchable = true, IsRetrievable = true, IsFilterable = true, IsFacetable = true },
                    new Field(nameof(Concert.Location), DataType.String, AnalyzerName.EnMicrosoft) { IsSearchable = true, IsRetrievable = true, IsFilterable = true, IsFacetable = true },
                    new Field(nameof(Concert.Title), DataType.String, AnalyzerName.EnMicrosoft) { IsSearchable = true, IsRetrievable = true },
                    new Field(nameof(Concert.Description), DataType.String, AnalyzerName.EnMicrosoft) { IsSearchable = true, IsRetrievable = true },
                    new Field(nameof(Concert.Price), DataType.Double) { IsSearchable = false, IsFilterable = true, IsFacetable = true, IsSortable = true, IsRetrievable = true },
                    new Field(nameof(Concert.StartTime), DataType.DateTimeOffset) { IsSearchable = false, IsRetrievable = true, IsSortable = true, IsFilterable = true },
                },
                Suggesters = new[]
                {
                    new Suggester("default-suggester", nameof(Concert.Artist), nameof(Concert.Location), nameof(Concert.Title))
                },
                DefaultScoringProfile = "default-scoring",
                ScoringProfiles = new[]
                {
                    new ScoringProfile("default-scoring")
                    {
                        // Add a lot of weight to the artist and above average weight to the title.
                        TextWeights = new TextWeights(new Dictionary<string, double> {
                            { nameof(Concert.Artist), 2.0 },
                            { nameof(Concert.Title), 1.5 }
                        })
                    }
                }
            };
            serviceClient.Indexes.CreateOrUpdate(concertsIndex);

            // Create the data source that connects to the SQL Database account containing the consult requests.
            var concertsDataSource = new DataSource
            {
                Name = IndexNameConcerts,
                Type = DataSourceType.AzureSql,
                Container = new DataContainer("Concerts"),
                Credentials = new DataSourceCredentials(this.concertsSqlDatabaseConnectionString),
                DataChangeDetectionPolicy = new SqlIntegratedChangeTrackingPolicy()
            };
            serviceClient.DataSources.CreateOrUpdate(concertsDataSource);

            // Create the indexer that will pull the data from the database into the search index.
            var concertsIndexer = new Indexer
            {
                Name = IndexNameConcerts,
                DataSourceName = IndexNameConcerts,
                TargetIndexName = IndexNameConcerts,
                Schedule = new IndexingSchedule(TimeSpan.FromMinutes(5))
            };
            serviceClient.Indexers.CreateOrUpdate(concertsIndexer);
        }

        #endregion

        #region Search

        public async Task<SearchResponse<ConcertSearchResult>> SearchAsync(SearchRequest request)
        {
            var query = request.Query;
            if (string.IsNullOrWhiteSpace(query))
            {
                query = "*";
            }
            var items = new List<ConcertSearchResult>();

            // Search concerts.
            var concertQueryParameters = new SearchParameters
            {
                // Highlight the search text in the description
                HighlightFields = new[] { nameof(Concert.Description) },

                // Sort on the requested field.
                OrderBy = GetOrderBy(request),

                // Filter on the requested fields.
                Filter = GetFilter(request),

                // Request facet information.
                Facets = new[] { $"{nameof(Concert.Price)},interval:{PriceFacetInterval}", nameof(Concert.Genre), nameof(Concert.Location) }
            };
            var concertResults = await this.concertsIndexClient.Documents.SearchAsync(query, concertQueryParameters);
            foreach (var concertResult in concertResults.Results)
            {
                items.Add(new ConcertSearchResult
                {
                    Score = concertResult.Score,
                    HitHighlights = concertResult.Highlights == null ? new string[0] : concertResult.Highlights.SelectMany(h => h.Value).ToArray(),
                    Id = int.Parse((string)concertResult.Document[nameof(Concert.Id)]),
                    Artist = (string)concertResult.Document[nameof(Concert.Artist)],
                    Genre = (string)concertResult.Document[nameof(Concert.Genre)],
                    Location = (string)concertResult.Document[nameof(Concert.Location)],
                    Title = (string)concertResult.Document[nameof(Concert.Title)],
                    Description = (string)concertResult.Document[nameof(Concert.Description)],
                    Price = (double)concertResult.Document[nameof(Concert.Price)],
                    StartTime = (DateTimeOffset)concertResult.Document[nameof(Concert.StartTime)]
                });
            }

            // Process the search facets.
            var facets = new List<SearchFacet>();
            foreach (var facetResultsForField in concertResults.Facets)
            {
                var fieldName = facetResultsForField.Key;
                var facetValues = facetResultsForField.Value.Select(f => GetFacetValue(fieldName, f)).ToArray();
                facets.Add(new SearchFacet(fieldName, fieldName, facetValues));
            }

            return new SearchResponse<ConcertSearchResult>(request, items, facets);
        }

        #endregion

        #region Suggest

        public async Task<ICollection<string>> SuggestAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new string[0];
            }
            var parameters = new SuggestParameters
            {
                Top = 10
            };
            var results = await this.concertsIndexClient.Documents.SuggestAsync(query, "default-suggester", parameters);
            return results.Results.Select(s => s.Text).Distinct().ToArray();
        }

        #endregion

        #region Helper Methods

        private static IList<string> GetOrderBy(SearchRequest request)
        {
            return new[] { request.SortOn + (request.SortDescending ? " desc" : "") };
        }

        private string GetFilter(SearchRequest request)
        {
            var filters = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.PriceRange))
            {
                var priceRangeStart = int.Parse(request.PriceRange);
                var priceRangeEnd = priceRangeStart + PriceFacetInterval;
                filters.Add($"({nameof(Concert.Price)} ge {priceRangeStart} and {nameof(Concert.Price)} lt {priceRangeEnd})");
            }
            if (!string.IsNullOrWhiteSpace(request.Genre))
            {
                filters.Add($"({nameof(Concert.Genre)} eq '{request.Genre}')");

            }
            if (!string.IsNullOrWhiteSpace(request.Location))
            {
                filters.Add($"({nameof(Concert.Location)} eq '{request.Location}')");

            }
            return string.Join(" and ", filters);
        }

        private static SearchFacetValue GetFacetValue(string fieldName, FacetResult facetResult)
        {
            var count = facetResult.Count ?? 0;
            if (string.Equals(fieldName, nameof(Concert.Price), StringComparison.OrdinalIgnoreCase))
            {
                var priceRangeStart = Convert.ToInt32(facetResult.Value);
                var priceRangeEnd = priceRangeStart + PriceFacetInterval - 1;
                var value = priceRangeStart.ToString();
                var displayName = $"{priceRangeStart.ToString("c0")} - {priceRangeEnd.ToString("c0")}";
                return new SearchFacetValue(value, displayName, count);
            }
            else
            {
                var value = (string)facetResult.Value;
                return new SearchFacetValue(value, value, count);
            }
        }

        #endregion
    }
}