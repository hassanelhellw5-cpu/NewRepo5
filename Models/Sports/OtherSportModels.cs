using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace QemmaProject.Models.Sports
{
    public class OtherSportEvent
    {
        public int Id { get; set; }
        public string SportKey { get; set; } = string.Empty; // formula1, tennis, basketball, etc.
        public string ExternalId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string CompetitionName { get; set; } = string.Empty;
        public DateTime EventDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Venue { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<OtherSportParticipant> Participants { get; set; } = new List<OtherSportParticipant>();
        public virtual ICollection<OtherSportResult> Results { get; set; } = new List<OtherSportResult>();
        public virtual ICollection<OtherSportLiveUpdate> LiveUpdates { get; set; } = new List<OtherSportLiveUpdate>();
        public virtual ICollection<OtherSportStream> Streams { get; set; } = new List<OtherSportStream>();
    }

    public class OtherSportParticipant
    {
        public int Id { get; set; }
        public int OtherSportEventId { get; set; }

        // Not required in incoming JSON � it's the circular parent reference,
        // fixed up automatically by EF Core once this entity is attached to
        // its parent's Participants collection. [ValidateNever] stops ASP.NET
        // Core's model validation from rejecting payloads that omit it;
        // [JsonIgnore] stops System.Text.Json from trying to (de)serialize a
        // circular graph on the way in or out.
        [ValidateNever]
        [JsonIgnore]
        public virtual OtherSportEvent? OtherSportEvent { get; set; }

        public string Name { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // driver, player, team, constructor
        public int? SeedOrNumber { get; set; }
        public string MetadataJson { get; set; } = "{}";
    }

    public class OtherSportResult
    {
        public int Id { get; set; }
        public int OtherSportEventId { get; set; }

        [ValidateNever]
        [JsonIgnore]
        public virtual OtherSportEvent? OtherSportEvent { get; set; }

        public string ParticipantName { get; set; } = string.Empty;
        public int? Rank { get; set; }
        public string Score { get; set; } = string.Empty;
        public string ResultText { get; set; } = string.Empty;
        public string MetadataJson { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class OtherSportLiveUpdate
    {
        public int Id { get; set; }
        public int OtherSportEventId { get; set; }

        [ValidateNever]
        [JsonIgnore]
        public virtual OtherSportEvent? OtherSportEvent { get; set; }

        public string ExternalId { get; set; } = string.Empty;
        public string MinuteOrLap { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    }

    public class OtherSportStream
    {
        public int Id { get; set; }
        public int OtherSportEventId { get; set; }

        [ValidateNever]
        [JsonIgnore]
        public virtual OtherSportEvent? OtherSportEvent { get; set; }

        public string Source { get; set; } = string.Empty;
        public string StreamUrl { get; set; } = string.Empty;
        public string M3U8Url { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}