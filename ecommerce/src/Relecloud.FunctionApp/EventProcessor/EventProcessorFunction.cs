using Azure;
using Azure.AI.TextAnalytics;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Relecloud.FunctionApp.EventProcessor
{
    public class EventProcessorFunction
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;

        public EventProcessorFunction(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<EventProcessorFunction>();
            _configuration = configuration;
        }

        [Function("EventProcessor")]
        public async Task Run(
            [QueueTrigger("relecloudconcertevents", Connection = "App:StorageAccount:ConnectionString")]
            Event eventInfo)
        {
            _logger.LogInformation($"Received event type \"{eventInfo.EventType}\" for entity \"{eventInfo.EntityId}\"");

            try
            {
                if ("TicketCreated".Equals(eventInfo.EventType, StringComparison.OrdinalIgnoreCase))
                {
                    var sqlDatabaseConnectionString = _configuration.GetValue<string>("App:SqlDatabase:ConnectionString");
                    if (int.TryParse(eventInfo.EntityId, out var ticketId)
                        && !string.IsNullOrWhiteSpace(sqlDatabaseConnectionString))
                    {
                        await CreateTicketImageAsync(ticketId);
                    }
                }
                else if ("ReviewCreated".Equals(eventInfo.EventType, StringComparison.OrdinalIgnoreCase))
                {
                    var sqlDatabaseConnectionString = _configuration.GetValue<string>("App:SqlDatabase:ConnectionString");
                    var cognitiveServicesEndpointUri = _configuration.GetValue<string>("App:CognitiveServices:EndpointUri");
                    var cognitiveServicesApiKey = _configuration.GetValue<string>("App:CognitiveServices:ApiKey");
                    if (int.TryParse(eventInfo.EntityId, out var reviewId)
                        && !string.IsNullOrWhiteSpace(sqlDatabaseConnectionString)
                        && !string.IsNullOrWhiteSpace(cognitiveServicesEndpointUri)
                        && !string.IsNullOrWhiteSpace(cognitiveServicesApiKey))
                    {
                        await CalculateReviewSentimentScoreAsync(sqlDatabaseConnectionString, cognitiveServicesEndpointUri, cognitiveServicesApiKey, reviewId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to process the TicketCreated event");
                throw;
            }
        }

        #region Create Ticket Image

        private async Task CreateTicketImageAsync(int ticketId)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new PlatformNotSupportedException("Ticket rendering must run on Windows environment");
            }

            var ticketImageBlob = new MemoryStream();
            using var connection = new SqlConnection(_configuration.GetValue<string>("App:SqlDatabase:ConnectionString"));

            // Retrieve the ticket from the database.
            _logger.LogInformation($"Retrieving details for ticket \"{ticketId}\" from SQL Database...");
            await connection.OpenAsync();
            var getTicketCommand = connection.CreateCommand();
            getTicketCommand.CommandText = "SELECT Concerts.Artist, Concerts.Location, Concerts.StartTime, Concerts.Price, Users.DisplayName FROM Tickets INNER JOIN Concerts ON Tickets.ConcertId = Concerts.Id INNER JOIN Users ON Tickets.UserId = Users.Id WHERE Tickets.Id = @id";
            getTicketCommand.Parameters.Add(new SqlParameter("id", ticketId));
            using (var ticketDataReader = await getTicketCommand.ExecuteReaderAsync())
            {
                // Get ticket details.
                var hasRows = await ticketDataReader.ReadAsync();
                if (hasRows == false)
                {
                    _logger.LogWarning($"No Ticket found for id:{ticketId}");
                    return; //this ticket was not found
                }

                var artist = ticketDataReader.GetString(0);
                var location = ticketDataReader.GetString(1);
                var startTime = ticketDataReader.GetDateTimeOffset(2);
                var price = ticketDataReader.GetDouble(3);
                var userName = ticketDataReader.GetString(4);

                // Generate the ticket image.
                using (var headerFont = new Font("Arial", 18, FontStyle.Bold))
                using (var textFont = new Font("Arial", 12, FontStyle.Regular))
                using (var bitmap = new Bitmap(640, 200, PixelFormat.Format24bppRgb))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(Color.White);

                    // Print concert details.
                    graphics.DrawString(artist, headerFont, Brushes.DarkSlateBlue, new PointF(10, 10));
                    graphics.DrawString($"{location}   |   {startTime.UtcDateTime}", textFont, Brushes.Gray, new PointF(10, 40));
                    graphics.DrawString($"{userName}   |   {price.ToString("c")}", textFont, Brushes.Gray, new PointF(10, 60));

                    // Print a fake barcode.
                    var random = new Random();
                    var offset = 15;
                    while (offset < 620)
                    {
                        var width = 2 * random.Next(1, 3);
                        graphics.FillRectangle(Brushes.Black, offset, 90, width, 90);
                        offset += width + (2 * random.Next(1, 3));
                    }

                    // Save to blob storage.
                    _logger.LogInformation("Uploading image to blob storage...");
                    bitmap.Save(ticketImageBlob, ImageFormat.Png);
                }
            }
            ticketImageBlob.Position = 0;
            _logger.LogInformation("Successfully generated image.");

            var storageAccountConnStr = _configuration.GetValue<string>("App:StorageAccount:ConnectionString");
            var blobServiceClient = new BlobServiceClient(storageAccountConnStr);

            //  Gets a reference to the container.
            var blobContainerClient = blobServiceClient.GetBlobContainerClient("tickets");

            //  Gets a reference to the blob in the container
            var blobClient = blobContainerClient.GetBlobClient($"ticket-{ticketId}.png");
            var blobInfo = await blobClient.UploadAsync(ticketImageBlob, overwrite: true);

            _logger.LogInformation("Successfully wrote blob to storage.");

            // Update the ticket in the database with the image URL.
            // Creates a client to the BlobService using the connection string.

            //  Defines the resource being accessed and for how long the access is allowed.
            var blobSasBuilder = new BlobSasBuilder
            {
                StartsOn = DateTime.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTime.UtcNow.Add(TimeSpan.FromDays(30)),
            };

            //  Defines the type of permission.
            blobSasBuilder.SetPermissions(BlobSasPermissions.Read);

            //  Builds the Sas URI.
            var queryUri = blobClient.GenerateSasUri(blobSasBuilder);

            _logger.LogInformation($"Updating ticket with image URL {queryUri}...");
            var updateTicketCommand = connection.CreateCommand();
            updateTicketCommand.CommandText = "UPDATE Tickets SET ImageUrl=@imageUrl WHERE Id=@id";
            updateTicketCommand.Parameters.Add(new SqlParameter("id", ticketId));
            updateTicketCommand.Parameters.Add(new SqlParameter("imageUrl", queryUri.ToString()));
            await updateTicketCommand.ExecuteNonQueryAsync();

            _logger.LogInformation("Successfully updated database with SAS.");
        }

        #endregion

        #region Calculate Review Sentiment Score

        private async Task CalculateReviewSentimentScoreAsync(string sqlDatabaseConnectionString, string cognitiveServicesEndpointUri, string cognitiveServicesApiKey, int reviewId)
        {
            using var connection = new SqlConnection(sqlDatabaseConnectionString);
            await connection.OpenAsync();

            // Retrieve the review description.
            _logger.LogInformation($"Retrieving description for review \"{reviewId}\" from SQL Database...");
            var getDescriptionCommand = connection.CreateCommand();
            getDescriptionCommand.CommandText = "SELECT Description FROM Reviews WHERE Id=@id";
            getDescriptionCommand.Parameters.Add(new SqlParameter("id", reviewId));
            var reviewDescription = (string?)await getDescriptionCommand.ExecuteScalarAsync();
            if (reviewDescription is null)
            {
                return; //there is no comment to analyze
            }

            // Perform a sentiment analysis on the text.
            // Scores close to 1 indicate positive sentiment, while scores close to 0 indicate negative sentiment.
            _logger.LogInformation($"Performing sentiment analysis on text: \"{reviewDescription}\"...");
            var sentimentScore = await GetSentimentScoreAsync(reviewDescription, cognitiveServicesEndpointUri, cognitiveServicesApiKey);

            // Update the document with the sentiment value.
            _logger.LogInformation($"Updating review with sentiment score {sentimentScore}...");
            var updateSentimentScoreCommand = connection.CreateCommand();
            updateSentimentScoreCommand.CommandText = "UPDATE Reviews SET SentimentScore=@sentimentScore WHERE Id=@id";
            updateSentimentScoreCommand.Parameters.Add(new SqlParameter("id", reviewId));
            updateSentimentScoreCommand.Parameters.Add(new SqlParameter("sentimentScore", sentimentScore));
            await updateSentimentScoreCommand.ExecuteNonQueryAsync();
        }

        private async Task<float> GetSentimentScoreAsync(string text, string cognitiveServicesEndpointUri, string cognitiveServicesApiKey)
        {
            var credentials = new AzureKeyCredential(cognitiveServicesApiKey);
            var client = new TextAnalyticsClient(new Uri(cognitiveServicesEndpointUri), credentials);
            var reviews = await client.AnalyzeSentimentAsync(text);
            return (float)reviews.Value.ConfidenceScores.Positive;
        }

        #endregion
    }
}
