using System.Timers;
using CompCube_Server.Interfaces;
using Timer = System.Timers.Timer;

namespace CompCube_Server.Models.Client;

public class MatchmakingClient(IConnectedClient client)
{
    public IConnectedClient Client { get; private set; } = client;

    public DateTime joinedTime = DateTime.Now;

    const int BaseRange = 50;
    const int ExpansionRate = 1; // add 1 mmr to base range every second a player isnt queued
    const int Cap = 400;

    public bool CanMatchWithOtherClient(MatchmakingClient other)
    {
        int thisExpansion = (int)(DateTime.Now - joinedTime).TotalSeconds * ExpansionRate;
        int otherExpansion = (int)(DateTime.Now - other.joinedTime).TotalSeconds * ExpansionRate;

        int thisRange = Math.Min(BaseRange + thisExpansion, Cap);
        int otherRange = Math.Min(BaseRange + otherExpansion, Cap);

        int allowedRange = Math.Max(thisRange, otherRange);

        int delta = Math.Abs(
            Client.UserProfile.CompetitiveStats.Elo - other.Client.UserProfile.CompetitiveStats.Elo);

        return delta <= allowedRange;
    }

}