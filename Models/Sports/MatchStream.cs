using System.Collections.Generic;

namespace QemmaProject.Models.Sports
{
    public class MatchStream
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string StreamUrl { get; set; }
        public string home_team { get; set; }

        public string away_team { get; set; }
        public string M3U8Url { get; set; }
        public string AltM3U8Url { get; set; }
        public string Source { get; set; }
    }
}
