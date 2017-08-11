using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace Relecloud.Web.Models
{
    public class Ticket
    {
        [Key]
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("concertId")]
        public int ConcertId { get; set; }

        [JsonProperty("userId")]
        public string UserId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("price")]
        public double Price { get; set; }
    }
}