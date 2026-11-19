using System;

/// <summary>Describes one learning action recorded by a gameplay system.</summary>
[Serializable]
public struct LearningEvent
{
    public LearningObjectiveId Objective;
    public LearningEventType Type;

    public LearningEvent(LearningObjectiveId objective, LearningEventType type)
    {
        Objective = objective;
        Type = type;
    }
}

public enum LearningEventType
{
    Attempted,
    Mistake,
    Completed,
    HintShown
}
