using UnityEngine;

/// <summary>Optional Resources/AdaptiveScenarioConfig asset for tuning adaptive hint thresholds.</summary>
[CreateAssetMenu(menuName = "What Should I Do/Adaptive Scenario Config")]
public sealed class AdaptiveScenarioConfig : ScriptableObject
{
    [Min(1)] public int gentleHintAfterMistakes = 1;
    [Min(2)] public int guidedHintAfterMistakes = 2;
}
