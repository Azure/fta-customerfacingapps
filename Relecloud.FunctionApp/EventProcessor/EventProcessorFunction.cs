using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Relecloud.FunctionApp.EventProcessor
{
    public static class EventProcessorFunction
    {
        public static async Task Run(byte[] eventMessage, TraceWriter log)
        {
            // The message body sent by the .NET Core Service Bus client is encoded and
            // cannot automatically be bound to a string or JSON object in a Function.
            var eventInfo = JsonConvert.DeserializeObject<Event>(Encoding.UTF8.GetString(eventMessage));
            log.Info($"Received event type \"{eventInfo.EventType}\" for entity \"{eventInfo.EntityId}\")");

            if (string.Equals(eventInfo.EventType, "ReviewCreated", StringComparison.OrdinalIgnoreCase))
            {
                var sqlDatabaseConnectionString = ConfigurationManager.AppSettings["App:SqlDatabase:ConnectionString"];
                var cognitiveServicesEndpointUri = ConfigurationManager.AppSettings["App:CognitiveServices:EndpointUri"];
                var cognitiveServicesApiKey = ConfigurationManager.AppSettings["App:CognitiveServices:ApiKey"];
                await CalculateReviewSentimentScoreAsync(sqlDatabaseConnectionString, cognitiveServicesEndpointUri, cognitiveServicesApiKey, eventInfo.EntityId, log);
            }
        }

        private static async Task CalculateReviewSentimentScoreAsync(string sqlDatabaseConnectionString, string cognitiveServicesEndpointUri, string cognitiveServicesApiKey, string reviewId, TraceWriter log)
        {
            using (var connection = new SqlConnection(sqlDatabaseConnectionString))
            {
                await connection.OpenAsync();

                // Retrieve the review description.
                log.Info($"Retrieving description for review \"{reviewId}\" from SQL Database...");
                var getDescriptionCommand = connection.CreateCommand();
                getDescriptionCommand.CommandText = "SELECT Description FROM Reviews WHERE Id=@id";
                getDescriptionCommand.Parameters.Add(new SqlParameter("id", reviewId));
                var reviewDescription = (string)await getDescriptionCommand.ExecuteScalarAsync();

                // Perform a sentiment analysis on the text.
                // Scores close to 1 indicate positive sentiment, while scores close to 0 indicate negative sentiment.
                log.Info($"Performing sentiment analysis on text: \"{reviewDescription}\"...");
                var sentimentScore = await GetSentimentScoreAsync(reviewDescription, cognitiveServicesEndpointUri, cognitiveServicesApiKey);

                // Update the document with the sentiment value.
                log.Info($"Updating review with sentiment score {sentimentScore}...");
                var updateSentimentScoreCommand = connection.CreateCommand();
                updateSentimentScoreCommand.CommandText = "UPDATE Reviews SET SentimentScore=@sentimentScore WHERE Id=@id";
                updateSentimentScoreCommand.Parameters.Add(new SqlParameter("id", reviewId));
                updateSentimentScoreCommand.Parameters.Add(new SqlParameter("sentimentScore", sentimentScore));
                await updateSentimentScoreCommand.ExecuteNonQueryAsync();
            }
        }

        private static async Task<float> GetSentimentScoreAsync(string text, string cognitiveServicesEndpointUri, string cognitiveServicesApiKey)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", cognitiveServicesApiKey);
                var request = new { documents = new[] { new { language = "en", id = "001", text = text } } };
                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(cognitiveServicesEndpointUri, content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync();
                var dynamicResponse = (dynamic)JsonConvert.DeserializeObject(responseBody);
                return (float)dynamicResponse.documents[0].score;
            }
        }
    }
}