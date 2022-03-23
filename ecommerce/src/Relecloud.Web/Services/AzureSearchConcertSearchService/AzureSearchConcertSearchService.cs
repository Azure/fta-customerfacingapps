using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Relecloud.Web.Infrastructure;
using Relecloud.Web.Models;

namespace Relecloud.Web.Services.AzureSearchService
{
    public class AzureSearchConcertSearchService : IConcertSearchService
    {
        #region Constants

        private const string IndexNameConcerts = "concerts";
        private const int PriceFacetInterval = 20;

        #endregion

        #region Fields

        private readonly Uri searchServiceUri;
        private readonly AzureKeyCredential azureKeyCredential;
        private readonly string concertsSqlDatabaseConnectionString;
        private readonly SearchClient concertsIndexClient;

        #endregion

        #region Constructors

        public AzureSearchConcertSearchService(string searchServiceName, string adminKey, string concertsSqlDatabaseConnectionString)
        {
            this.searchServiceUri = new Uri($"https://{searchServiceName}.search.windows.net");
            this.concertsSqlDatabaseConnectionString = concertsSqlDatabaseConnectionString;
            this.azureKeyCredential = new AzureKeyCredential(adminKey);
            this.concertsIndexClient = new SearchClient(this.searchServiceUri, IndexNameConcerts, this.azureKeyCredential);
        }

        #endregion

        #region Initialize

        public void Initialize()
        {
            var serviceClient = new SearchIndexClient(this.searchServiceUri, this.azureKeyCredential);
            InitializeConcertsIndex(serviceClient);
        }

        private void InitializeConcertsIndex(SearchIndexClient serviceClient)
        {
            // Create the index that will contain the searchable data from the concerts.
            var concertsIndex = new SearchIndex(IndexNameConcerts)
            {
                Fields = new[]
                {
                    new SearchField(nameof(Concert.Id), SearchFieldDataType.String)
                    {
                        IsKey = true,
                        IsSearchable = false
                    },
                    new SearchField(nameof(Concert.Artist), SearchFieldDataType.String)
                    {
                        AnalyzerName = LexicalAnalyzerName.EnMicrosoft,
                        IsSearchable = true,
                    },
                    new SearchField(nameof(Concert.Genre), SearchFieldDataType.String)
                    {
                        AnalyzerName = LexicalAnalyzerName.EnMicrosoft,
                        IsSearchable = true,
                        IsFilterable = true,
                        IsFacetable = true
                    },
                    new SearchField(nameof(Concert.Location), SearchFieldDataType.String)
                    {
                        AnalyzerName = LexicalAnalyzerName.EnMicrosoft,
                        IsSearchable = true,
                        IsFilterable = true,
                        IsFacetable = true
                    },
                    new SearchField(nameof(Concert.Title), SearchFieldDataType.String) {
                        AnalyzerName = LexicalAnalyzerName.EnMicrosoft,
                        IsSearchable = true,
                    },
                    new SearchField(nameof(Concert.Description), SearchFieldDataType.String) {
                        AnalyzerName = LexicalAnalyzerName.EnMicrosoft,
                        IsSearchable = true,
                    },
                    new SearchField(nameof(Concert.Price), SearchFieldDataType.Double)
                    {
                        IsSearchable = false,
                        IsFilterable = true,
                        IsFacetable = true,
                        IsSortable = true,
                    },
                    new SearchField(nameof(Concert.StartTime), SearchFieldDataType.DateTimeOffset)
                    {
                        IsSearchable = false,
                        IsSortable = true,
                        IsFilterable = true
                    },
                },
                DefaultScoringProfile = "default-scoring",
            };

            var suggester = new SearchSuggester("default-suggester", new[] { nameof(Concert.Artist), nameof(Concert.Location), nameof(Concert.Title) });
            concertsIndex.Suggesters.Add(suggester);
            concertsIndex.ScoringProfiles.Add(new ScoringProfile("default-scoring")
            {
                // Add a lot of weight to the artist and above average weight to the title.
                TextWeights = new TextWeights(new Dictionary<string, double> {
                    { nameof(Concert.Artist), 2.0 },
                    { nameof(Concert.Title), 1.5 }
                })
            });

            serviceClient.CreateOrUpdateIndex(concertsIndex);

            var searchIndexerClient = new SearchIndexerClient(this.searchServiceUri, this.azureKeyCredential);

            // Create the data source that connects to the SQL Database account containing the consult requests.
            var concertsDataSource = new SearchIndexerDataSourceConnection(IndexNameConcerts, SearchIndexerDataSourceType.AzureSql, this.concertsSqlDatabaseConnectionString, new SearchIndexerDataContainer("Concerts"))
            {
                DataChangeDetectionPolicy = new SqlIntegratedChangeTrackingPolicy()
            };

            searchIndexerClient.CreateOrUpdateDataSourceConnection(concertsDataSource);

            // Create the indexer that will pull the data from the database into the search index.
            var concertsIndexer = new SearchIndexer(name: IndexNameConcerts, dataSourceName: IndexNameConcerts, targetIndexName: IndexNameConcerts)
            {
                Schedule = new IndexingSchedule(TimeSpan.FromMinutes(5))
            };

            searchIndexerClient.CreateOrUpdateIndexer(concertsIndexer);
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
            var concertQueryParameters = new SearchOptions();


            concertQueryParameters.HighlightFields.Add(nameof(Concert.Description));
            concertQueryParameters.OrderBy.AddRange(GetOrderBy(request));
            concertQueryParameters.Facets.AddRange(new[] { $"{nameof(Concert.Price)},interval:{PriceFacetInterval}", nameof(Concert.Genre), nameof(Concert.Location) });
            concertQueryParameters.Filter = GetFilter(request);

            var concertResults = await this.concertsIndexClient.SearchAsync<ConcertSearchResult>(query, concertQueryParameters);

            foreach (var concertResult in concertResults.Value.GetResults())
            {
                concertResult.Document.HitHighlights = concertResult.Highlights.SelectMany(h => h.Value).ToArray();
                items.Add(concertResult.Document);
            }

            // Process the search facets.
            var facets = new List<SearchFacet>();
            foreach (var facetResultsForField in concertResults.Value.Facets)
            {
                var fieldName = facetResultsForField.Key;
                var facetValues = facetResultsForField.Value.Select(f => GetFacetValue(fieldName, f)).ToArray();
                facets.Add(new SearchFacet(fieldName, fieldName, facetValues));
            }

            var searchResponse = new SearchResponse<ConcertSearchResult>(request, items, facets);

            return searchResponse;
        }

        #endregion

        #region Suggest

        public async Task<ICollection<string>> SuggestAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return new string[0];
            }

            var options = new AutocompleteOptions()
            {
                Mode = AutocompleteMode.OneTermWithContext,
                Size = 10
            };

            // Convert the autocompleteResult results to a list that can be displayed in the client.
            var autocompleteResult = await this.concertsIndexClient.AutocompleteAsync(query, "default-suggester", options).ConfigureAwait(false);

            return autocompleteResult.Value.Results.Select(x => x.Text).ToList();
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