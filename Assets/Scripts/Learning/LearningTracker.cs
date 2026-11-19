using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persists objective-level learning events locally in browser PlayerPrefs.</summary>
internal static class LearningTracker
{
    const string PreferenceKey = "wsid.learning.profile.v1";
    static readonly Dictionary<LearningObjectiveId, LearningObjectiveProgress> Records =
        new Dictionary<LearningObjectiveId, LearningObjectiveProgress>();
    static bool loaded;

    internal static void RecordEvent(LearningEvent learningEvent)
    {
        EnsureLoaded();
        LearningObjectiveProgress progress = GetOrCreate(learningEvent.Objective);

        switch (learningEvent.Type)
        {
            case LearningEventType.Attempted:
                progress.attempts++;
                break;
            case LearningEventType.Mistake:
                progress.mistakes++;
                progress.consecutiveMistakes++;
                break;
            case LearningEventType.Completed:
                progress.completed = true;
                progress.consecutiveMistakes = 0;
                break;
            case LearningEventType.HintShown:
                progress.hintsShown++;
                break;
        }

        Save();
    }

    internal static int ConsecutiveMistakes(LearningObjectiveId objective)
    {
        EnsureLoaded();
        return GetOrCreate(objective).consecutiveMistakes;
    }

    internal static bool IsCompleted(LearningObjectiveId objective)
    {
        EnsureLoaded();
        return GetOrCreate(objective).completed;
    }

    internal static int CompletedCount(GameLevel level)
    {
        EnsureLoaded();
        int count = 0;
        foreach (LearningObjectiveId objective in ObjectivesFor(level))
            if (GetOrCreate(objective).completed)
                count++;
        return count;
    }

    internal static int ObjectiveCount(GameLevel level) => ObjectivesFor(level).Length;

    internal static void ResetAll()
    {
        Records.Clear();
        loaded = true;
        PlayerPrefs.DeleteKey(PreferenceKey);
        PlayerPrefs.Save();
    }

    static LearningObjectiveId[] ObjectivesFor(GameLevel level)
    {
        switch (level)
        {
            case GameLevel.Crossing:
                return new[]
                {
                    LearningObjectiveId.Crossing_UseZebra,
                    LearningObjectiveId.Crossing_LookBothWays,
                    LearningObjectiveId.Crossing_WaitForTraffic,
                };
            case GameLevel.School:
                return new[]
                {
                    LearningObjectiveId.School_CheckStudent,
                    LearningObjectiveId.School_FindTeacher,
                    LearningObjectiveId.School_ReportIncident,
                    LearningObjectiveId.School_ReportLocation,
                    LearningObjectiveId.School_LeadTeacher,
                };
            default:
                return new[]
                {
                    LearningObjectiveId.Playground_Invite,
                    LearningObjectiveId.Playground_MakeRoom,
                    LearningObjectiveId.Playground_ShareTurns,
                };
        }
    }

    static LearningObjectiveProgress GetOrCreate(LearningObjectiveId objective)
    {
        if (!Records.TryGetValue(objective, out LearningObjectiveProgress progress))
        {
            progress = new LearningObjectiveProgress { objectiveId = objective.ToString() };
            Records.Add(objective, progress);
        }

        return progress;
    }

    static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        string json = PlayerPrefs.GetString(PreferenceKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            LearningProfile profile = JsonUtility.FromJson<LearningProfile>(json);
            if (profile?.objectives == null)
                return;

            foreach (LearningObjectiveProgress progress in profile.objectives)
            {
                if (progress == null || !Enum.TryParse(progress.objectiveId, out LearningObjectiveId objective))
                    continue;

                Records[objective] = progress;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[LearningTracker] Could not read the saved learning profile: " + exception.Message);
        }
    }

    static void Save()
    {
        var profile = new LearningProfile
        {
            schemaVersion = 1,
            objectives = new List<LearningObjectiveProgress>(Records.Values).ToArray(),
        };
        PlayerPrefs.SetString(PreferenceKey, JsonUtility.ToJson(profile));
        PlayerPrefs.Save();
    }
}
