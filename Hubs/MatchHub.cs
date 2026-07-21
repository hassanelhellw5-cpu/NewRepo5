using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace QemmaProject.Hubs
{
    public class MatchHub : Hub
    {
        public static string MatchRoom(int matchId) => $"match:{matchId}";
        public static string WatchPartyRoom(string code) => $"watch-party:{code.ToUpperInvariant()}";
        public static string OtherSportRoom(int eventId) => $"other-sport:{eventId}";

        public async Task JoinMatchRoom(int matchId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, MatchRoom(matchId));
        }

        public async Task LeaveMatchRoom(int matchId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, MatchRoom(matchId));
        }

        public async Task JoinOtherSportRoom(int eventId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, OtherSportRoom(eventId));
        }

        public async Task LeaveOtherSportRoom(int eventId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OtherSportRoom(eventId));
        }

        public async Task JoinWatchPartyRoom(string code)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, WatchPartyRoom(code));
        }

        public async Task LeaveWatchPartyRoom(string code)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, WatchPartyRoom(code));
        }

        public async Task SendWatchPartyMessage(string code, object message)
        {
            await Clients.Group(WatchPartyRoom(code)).SendAsync("ReceiveWatchPartyMessage", message);
        }

        public async Task SendWatchPartyReaction(string code, object reaction)
        {
            await Clients.Group(WatchPartyRoom(code)).SendAsync("ReceiveWatchPartyReaction", reaction);
        }

        public async Task SendWatchPartySync(string code, object syncState)
        {
            await Clients.Group(WatchPartyRoom(code)).SendAsync("ReceiveWatchPartySync", syncState);
        }

        public async Task SendMatchReaction(int matchId, object reaction)
        {
            await Clients.Group(MatchRoom(matchId)).SendAsync("ReceiveMatchReaction", reaction);
        }

        public async Task SendMatchUpdate(object matchUpdate)
        {
            await Clients.All.SendAsync("ReceiveMatchUpdate", matchUpdate);
        }

        public async Task SendGoalNotification(string message)
        {
            await Clients.All.SendAsync("ReceiveGoal", message);
        }
    }
}
