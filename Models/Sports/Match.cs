using System;
using System.Collections.Generic;

namespace QemmaProject.Models.Sports
{
    public enum MatchStatus
    {
        NotStarted = 0,
        Live = 1,
        Finished = 2,
        Postponed = 3
    }

    public class Match
    {
        public int Id { get; set; }
        public string MatchId { get; set; } // Unique identifier from scraper
        // Removed ApiFixtureId to avoid api-football dependency
        public int HomeTeamId { get; set; }
        public virtual Team HomeTeam { get; set; }
        public int AwayTeamId { get; set; }
        public virtual Team AwayTeam { get; set; }
        public DateOnly MatchDate { get; set; }
        public string Status { get; set; }
        public string ScoreHome { get; set; }
        public string ScoreAway { get; set; }
        public string Time { get; set; }
        public int? TournamentId { get; set; }
        public string Channel { get; set; }
        public string SourceUrl { get; set; }

        // NEW: squad/lineup info
        public string HomeFormation { get; set; } // e.g. "4-4-2"
        public string AwayFormation { get; set; }
        public string HomeCoach { get; set; }
        public string AwayCoach { get; set; }

        public virtual Tournament Tournament { get; set; }
        public virtual ICollection<Prediction> Predictions { get; set; }
        public virtual ICollection<MatchEvent> Events { get; set; }
        public virtual ICollection<MatchStream> Streams { get; set; }
        public virtual ICollection<MatchLineup> Lineups { get; set; } // NEW
        public virtual ICollection<MatchVideo> Videos { get; set; }
    }
}