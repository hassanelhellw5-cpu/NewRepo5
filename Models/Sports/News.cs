namespace QemmaProject.Models.Sports
{
    public class News
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public DateTime PublishedAt { get; set; }
        public string Url { get; set; }
        public string? Category { get; set; }
        public string? SportKey { get; set; }
        public string? Source { get; set; }
        public string? Tags { get; set; }

        // ربط الخبر ببطولة أو مباراة معينة (Nullable لأن الخبر قد يكون عاماً)
        public int? TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }

        public int? MatchId { get; set; }
        public virtual Match Match { get; set; }
    }
}
