using System;

/// <summary>Serializable local learning history. It contains no player identity or personal data.</summary>
[Serializable]
public sealed class LearningProfile
{
    public int schemaVersion = 1;
    public LearningObjectiveProgress[] objectives = Array.Empty<LearningObjectiveProgress>();
}

[Serializable]
public sealed class LearningObjectiveProgress
{
    public string objectiveId;
    public int attempts;
    public int mistakes;
    public int consecutiveMistakes;
    public int hintsShown;
    public bool completed;
}
