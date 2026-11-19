using UnityEngine;

/// <summary>Chooses progressively clearer hints from deterministic, authored learning rules.</summary>
internal static class AdaptiveLearningManager
{
    static AdaptiveScenarioConfig config;
    static bool configLoaded;

    internal static string HintFor(LearningObjectiveId objective, string defaultHint)
    {
        EnsureConfig();
        int mistakes = LearningTracker.ConsecutiveMistakes(objective);
        int gentleThreshold = config != null ? config.gentleHintAfterMistakes : 1;
        int guidedThreshold = config != null ? config.guidedHintAfterMistakes : 2;

        if (mistakes >= guidedThreshold)
            return GameLanguage.Text(GuidedHint(objective));

        // Keep the original, context-specific hint on the first mistake.
        // Escalate to the explicit step-by-step hint on repeated mistakes.
        if (mistakes >= gentleThreshold)
            return GameLanguage.Text(defaultHint);

        return GameLanguage.Text(defaultHint);
    }

    static string GuidedHint(LearningObjectiveId objective)
    {
        switch (objective)
        {
            case LearningObjectiveId.Crossing_UseZebra:
                return "Look for the white stripes. They show where people should cross.";
            case LearningObjectiveId.Crossing_LookBothWays:
                return "Stop at the edge, look both ways, and listen before crossing.";
            case LearningObjectiveId.Crossing_WaitForTraffic:
                return "Wait until approaching vehicles have stopped or the road is clear.";
            case LearningObjectiveId.School_CheckStudent:
                return "Listen to your classmate and check whether they need an adult's help.";
            case LearningObjectiveId.School_FindTeacher:
                return "Look for the teacher near the school office.";
            case LearningObjectiveId.School_ReportIncident:
                return "Listen to the student's words and choose the matching incident.";
            case LearningObjectiveId.School_ReportLocation:
                return "Choose the bench by the front gate as the location.";
            case LearningObjectiveId.School_LeadTeacher:
                return "Lead the teacher along the path back to your friend.";
            case LearningObjectiveId.Playground_Invite:
                return "Invite the classmate, then wait while they join the group.";
            case LearningObjectiveId.Playground_MakeRoom:
                return "Move onto the open ring so everyone has room.";
            default:
                return "Choose a classmate who has not received the ball yet.";
        }
    }

    static void EnsureConfig()
    {
        if (configLoaded)
            return;

        configLoaded = true;
        config = Resources.Load<AdaptiveScenarioConfig>("AdaptiveScenarioConfig");
    }
}
