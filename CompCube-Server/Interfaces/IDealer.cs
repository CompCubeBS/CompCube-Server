using CompCube_Models.Models.Map;

namespace CompCube_Server.Interfaces;

public interface IDealer
{
    public CCBeatmapData[] PullNewCards(int count);

    public void AddDiscarded(CCBeatmapData[] disallowedMaps);

    public CCBeatmapData[] CompleteDeck(CCBeatmapData[] currentDeck, int targetSize);
}