namespace Relecloud.Web.Models
{
    public class ConcertSearchResult
    {
        public string Id { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public DateTimeOffset StartTime { get; set; }

        public double Score { get; set; }
        public IList<string> HitHighlights { get; set; }
    }
}