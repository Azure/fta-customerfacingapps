using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;
using Relecloud.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Relecloud.Web.Services.CosmosDBConsultRequestRepository
{
    public class CosmosDBTicketRepository : ITicketRepository
    {
        private readonly string databaseId;
        private readonly string collectionId;
        private DocumentClient client;
        private Uri collectionUri;

        public CosmosDBTicketRepository(string databaseUri, string databaseKey, string databaseId, string collectionId)
        {
            this.databaseId = databaseId;
            this.collectionId = collectionId;
            this.client = new DocumentClient(new Uri(databaseUri), databaseKey);
            this.collectionUri = UriFactory.CreateDocumentCollectionUri(this.databaseId, this.collectionId);
        }

        public void Initialize()
        {
            this.client.OpenAsync().Wait();
            this.client.CreateDatabaseIfNotExistsAsync(new Database { Id = this.databaseId }).Wait();
            this.client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(this.databaseId), new DocumentCollection { Id = this.collectionId }).Wait();
        }

        public async Task CreateAsync(Ticket ticket)
        {
            await this.client.CreateDocumentAsync(this.collectionUri, ticket);
        }

        public async Task<IList<Ticket>> GetAllAsync(string userId)
        {
            var results = new List<Ticket>();
            var query = this.client.CreateDocumentQuery<Ticket>(this.collectionUri, new FeedOptions { MaxItemCount = 50 }).Where(t => t.UserId == userId).AsDocumentQuery();
            while (query.HasMoreResults)
            {
                var batch = await query.ExecuteNextAsync<Ticket>();
                results.AddRange(batch);
            }
            return results;
        }
    }
}