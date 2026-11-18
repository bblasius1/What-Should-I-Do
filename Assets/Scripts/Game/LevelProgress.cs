using UnityEngine;

internal enum GameLevel { Crossing, School, Playground }

/// <summary>Stores the highest completed score independently from the current run.</summary>
internal static class LevelProgress
{
    internal const int MaximumStars = 3;

    internal static GameLevel? FirstUnfinished()
    {
        for (int index = 0; index <= (int)GameLevel.Playground; index++)
            if (Best((GameLevel)index) == 0)
                return (GameLevel)index;
        return null;
    }

    internal static int Best(GameLevel level) => Mathf.Clamp(PlayerPrefs.GetInt(PreferenceKey(level)), 0, MaximumStars);

    internal static int Record(GameLevel level, int stars)
    {
        int best = Best(level);
        int score = Mathf.Clamp(stars, 1, MaximumStars);
        if (score <= best)
            return best;

        PlayerPrefs.SetInt(PreferenceKey(level), score);
        PlayerPrefs.Save();
        return score;
    }

    internal static int StarsForMistakes(int mistakes) => mistakes <= 0 ? 3 : mistakes <= 2 ? 2 : 1;

    internal static string Icons(int stars) =>
        new string('★', Mathf.Clamp(stars, 0, MaximumStars)) +
        new string('☆', MaximumStars - Mathf.Clamp(stars, 0, MaximumStars));

    internal static string Title(GameLevel level) => level switch
    {
        GameLevel.Crossing => "Cross Safely",
        GameLevel.School => "Get Help at the School Gate",
        _ => "Make Room for Everyone",
    };

    static string PreferenceKey(GameLevel level) => $"wsid.progress.{(int)level}.bestStars";
}
