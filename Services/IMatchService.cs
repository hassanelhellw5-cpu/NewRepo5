using QemmaProject.Models;
using QemmaProject.Models.Sports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QemmaProject.Services
{
    public interface IMatchService
    {
        Task<List<string>> SaveMatchesFromScraperAsync(List<YallaKoraTournamentDto> tournamentsDto);
        Task<List<string>> SaveLiveStreamsFromScraperAsync(List<LiveStreamDto> streamsDto);
        Task SaveNewsFromScraperAsync(List<NewsDto> newsDto);
        Task SaveTournamentDetailsFromScraperAsync(string tournamentId, QemmaProject.Models.TournamentDetailsDto detailsDto);
        Task<string> SaveMatchSquadFromScraperAsync(MatchSquadDto squadDto);
        Task<string> SaveMatchDetailsFromScraperAsync(MatchDetailsDto detailsDto);
        Task<string> SaveMatchVideosFromScraperAsync(MatchVideosImportDto videosDto);
        Task<List<MatchVideo>> GetMatchVideosAsync(string matchId);
        Task<Match> GetMatchSquadAsync(string matchId);
        Task<List<TournamentBracket>> GetBracketAsync(int tournamentId);
        Task<List<Match>> GetMatchesByDateAsync(DateTime date);
        Task<List<Match>> GetAllMatchesAsync();
        Task<List<News>> GetLatestNewsAsync(int take = 20, string? category = null, string? sportKey = null);
        Task<List<StandingEntry>> GetStandingsAsync(int tournamentId);
        Task<List<PlayerScorer>> GetScorersAsync(int tournamentId);
    }
}
