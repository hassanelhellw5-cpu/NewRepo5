using System.Collections.Generic;

namespace QemmaProject.Models
{
    public class YallaKoraTournamentDto
    {
        public string tournament_name { get; set; }
        public string tournament_id { get; set; }
        public string? tournament_logo { get; set; }
        public List<YallaKoraMatchDto> matches { get; set; }
    }

    public class YallaKoraMatchDto
    {
        public string home_team { get; set; }
        public string away_team { get; set; }
        public string? home_team_logo { get; set; }
        public string? away_team_logo { get; set; }
        public string score_home { get; set; }
        public string score_away { get; set; }
        public string time { get; set; }
        public string? match_date { get; set; }
        public string status { get; set; }
        public string match_id { get; set; }
        public string? channel { get; set; }
        public string? match_href { get; set; }
    }

    public class LiveStreamDto
    {
        public string match_name { get; set; }
        public string match_id { get; set; }
        public string home_team { get; set; }
        public string away_team { get; set; }
        public string source { get; set; }
        public string stream_url { get; set; }
        public string m3u8_url { get; set; }
        public string alt_m3u8_url { get; set; }
        public string? status_message { get; set; }
    }

    public class NewsDto
    {
        public string title { get; set; }
        public string description { get; set; }
        public string? content { get; set; }
        public string image_url { get; set; }
        public string url { get; set; }
        public string published_at { get; set; }
        public string? source { get; set; }
        public string? sport_key { get; set; }
        public string? category { get; set; }
        public string? tags { get; set; }
    }

    public class TournamentDetailsDto
    {
        public List<StandingEntryDto> standings { get; set; } = new List<StandingEntryDto>();
        public List<PlayerScorerDto> scorers { get; set; } = new List<PlayerScorerDto>();
        public List<BracketMatchDto> bracket { get; set; } = new List<BracketMatchDto>(); // NEW
    }

    public class MatchDetailsDto
    {
        public string match_id { get; set; }
        public List<MatchEventDto> events { get; set; } = new List<MatchEventDto>();
        public List<MatchStatisticDto> stats { get; set; } = new List<MatchStatisticDto>();
    }

    public class MatchEventDto
    {
        public string external_id { get; set; }
        public string minute { get; set; }
        public string type { get; set; }
        public string player_name { get; set; }
        public string assist_player_name { get; set; }
        public string detail { get; set; }
        public string team_side { get; set; }
        public string published_at { get; set; }
    }

    public class MatchStatisticDto
    {
        public string name { get; set; }
        public string home_value { get; set; }
        public string away_value { get; set; }
    }

    public class MatchVideosImportDto
    {
        public string match_id { get; set; }
        public List<MatchVideoDto> videos { get; set; } = new List<MatchVideoDto>();
    }

    public class MatchVideoDto
    {
        public string external_id { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string video_url { get; set; }
        public string embed_url { get; set; }
        public string thumbnail_url { get; set; }
        public string source { get; set; }
        public string type { get; set; }
        public string published_at { get; set; }
    }

    public class StandingEntryDto
    {
        public string team { get; set; }
        public string group { get; set; } // تم إضافة حقل المجموعة هنا
        public int rank { get; set; }
        public int played { get; set; }
        public int points { get; set; }
    }

    public class PlayerScorerDto
    {
        public string player { get; set; }
        public string team { get; set; }
        public int goals { get; set; }
        public int assists { get; set; } // تم إضافة حقل الأسيست هنا
    }

    // NEW: knockout bracket match
    public class BracketMatchDto
    {
        public string round { get; set; }       // e.g. "Round of 16", "Semi-final", "Final"
        public string home_team { get; set; }
        public string away_team { get; set; }
        public string score { get; set; }
        public string winner { get; set; }
        public int order { get; set; }          // display order within the round
    }

    // NEW: match squad / lineup
    public class MatchSquadDto
    {
        public string match_id { get; set; }
        public string home_formation { get; set; }
        public string away_formation { get; set; }
        public string home_coach { get; set; }
        public string away_coach { get; set; }
        public List<SquadPlayerDto> home_main { get; set; } = new List<SquadPlayerDto>();
        public List<SquadPlayerDto> home_sub { get; set; } = new List<SquadPlayerDto>();
        public List<SquadPlayerDto> away_main { get; set; } = new List<SquadPlayerDto>();
        public List<SquadPlayerDto> away_sub { get; set; } = new List<SquadPlayerDto>();
    }

    public class SquadPlayerDto
    {
        public string name { get; set; }
        public string number { get; set; }
        public string position { get; set; }
    }
}