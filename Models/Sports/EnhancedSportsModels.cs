using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QemmaProject.Models.Sports
{
    public class Tournament
    {
        public int Id { get; set; }
        public string YallaKoraId { get; set; }
        public string Name { get; set; }
        public string? LogoUrl { get; set; }
        public string Type { get; set; } = "League"; // League, Tournament (Groups+Knockout)
        
        public virtual ICollection<Match> Matches { get; set; }
        public virtual ICollection<Standing> Standings { get; set; }
        public virtual ICollection<StandingEntry> StandingEntries { get; set; }
        public virtual ICollection<PlayerScorer> Scorers { get; set; }
        public virtual ICollection<TournamentBracket> Brackets { get; set; }
    }

    public class MatchEvent
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string Minute { get; set; }
        public string Type { get; set; } // Goal, Card, Substitution, MinuteByMinute
        public string PlayerName { get; set; }
        public string AssistPlayerName { get; set; }
        public string Detail { get; set; } // event text / articleBody
        public string TeamSide { get; set; } // Home or Away / empty for neutral live updates
        public string? ExternalId { get; set; }
        public DateTime? PublishedAt { get; set; }
    }

    public class MatchStatistic
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string Name { get; set; } = string.Empty;
        public string HomeValue { get; set; } = string.Empty;
        public string AwayValue { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MatchVideo
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string EmbedUrl { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public string Source { get; set; } = "YallaKora";
        public string Type { get; set; } = "Other"; // Summary, Goals, SingleGoal, Penalties, Other
        public DateTime? PublishedAt { get; set; }
        public bool IsAvailable { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PlayerScorer
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        public string PlayerName { get; set; }
        public string TeamName { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int Rank { get; set; }
    }

    public class StandingEntry
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        public string GroupName { get; set; } // e.g., Group A
        public int Rank { get; set; }
        public string TeamName { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points { get; set; }
    }

    public class TournamentBracket
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        public string RoundName { get; set; } // e.g., Round of 16, Quarter-final, Semi-final, Final
        public string TeamHomeName { get; set; }
        public string TeamAwayName { get; set; }
        public string Score { get; set; }
        public string WinnerName { get; set; }
        public int MatchOrder { get; set; }
    }
}
