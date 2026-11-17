using CompCube_Models.Models.Map;

namespace CompCube_Server.Interfaces;

public interface IDealer
{
    public BeatmapData[] PullNewCards(int count);

    public void AddDiscarded(BeatmapData[] disallowedMaps);

    public BeatmapData[] CompleteDeck(BeatmapData[] currentDeck, int targetSize);
}