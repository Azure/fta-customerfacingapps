namespace Relecloud.Web.Models
{
    public class SearchRequest
    {
        public string Query { get; set; }
        public string SortOn { get; set; }
        public bool SortDescending { get; set; }
        public string PriceRange { get; set; }
        public string Genre { get; set; }
        public string Location { get; set; }

        public SearchRequest Clone()
        {
            return new SearchRequest
            {
                Query = this.Query,
                SortOn = this.SortOn,
                SortDescending = this.SortDescending,
                PriceRange = this.PriceRange,
                Genre = this.Genre,
                Location = this.Location
            };
        }
    }
}