using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A short schoolyard exercise in finding a teacher and reporting an incident.</summary>
[DisallowMultipleComponent]
public sealed class SchoolHelpStage : MonoBehaviour
{
    enum Phase { Inactive, CheckStudent, FindTeacher, ReportIncident, ReportLocation, LeadTeacher, Complete }

    const float TalkRadius = 1.9f;
    const float NameRadius = 4.5f;
    const float EdgeClearance = 0.32f;
    const float TeacherFollowingDistance = 1.35f;
    const float TeacherSpeed = 3.9f;
    const float TeacherAcceleration = 8f;
    const float TeacherTurnSpeed = 540f;
    const float TrailSpacing = 0.65f;

    static readonly string[] IncidentAnswers =
    {
        "A student lost their bag.",
        "A student tripped and fell.",
        "Two students were arguing."
    };

    static readonly string[] LocationAnswers =
    {
        "By the school office door.",
        "By the courtyard planter.",
        "Beside the bench by the front gate."
    };

    readonly Queue<Vector3> trail = new Queue<Vector3>();
    SchoolYardLayout yard;
    SchoolNpc student;
    SchoolNpc teacher;
    SchoolNpc classmate;
    SchoolNpc conversationPartner;
    PlayerWalker player;
    QuizUi ui;
    Camera viewCamera;
    Action<int> completed;
    Phase phase;
    bool inDialogue;
    Vector3 lastTrailPoint;
    float escortSpeed;
    float teacherArrivalSide;
    int mistakes;

    internal void Begin(PlayerWalker walker, QuizUi quizUi, Camera camera, Action<int> onCompleted)
    {
        Stop();

        player = walker;
        ui = quizUi;
        viewCamera = camera;
        completed = onCompleted;
        yard = SchoolYardLayout.Create(transform);

        student = SchoolNpc.Create(yard.Root, SchoolNpcRole.Student, yard.StudentPosition,
            Quaternion.LookRotation(Vector3.right));
        teacher = SchoolNpc.Create(yard.Root, SchoolNpcRole.Teacher, yard.TeacherPosition,
            Quaternion.LookRotation(Vector3.back));
        classmate = SchoolNpc.Create(yard.Root, SchoolNpcRole.Classmate, yard.OtherPersonPosition,
            Quaternion.LookRotation(Vector3.left));
        student.SetCalling(true);

        player.ResetTo(yard.SpawnPoint, Quaternion.identity);
        phase = Phase.CheckStudent;
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L2_NAR_001");
        ui.SetVisible(true);
        ui.HideModal();
        player.enabled = true;
    }

    internal void Stop()
    {
        StopAllCoroutines();
        phase = Phase.Inactive;
        inDialogue = false;
        completed = null;
        conversationPartner = null;
        trail.Clear();
        escortSpeed = 0f;
        teacherArrivalSide = 0f;
        mistakes = 0;

        if (yard != null)
        {
            yard.Root.gameObject.SetActive(false);
            Destroy(yard.Root.gameObject);
            yard = null;
        }
    }

    internal Vector3 ConstrainMovement(Vector3 position, Vector3 step)
    {
        if (yard == null)
            return step;

        Vector3 destination = position + step;
        Bounds bounds = yard.WalkableBounds;
        destination.x = Mathf.Clamp(destination.x, bounds.min.x + EdgeClearance,
            bounds.max.x - EdgeClearance);
        destination.z = Mathf.Clamp(destination.z, bounds.min.z + EdgeClearance,
            bounds.max.z - EdgeClearance);
        return destination - position;
    }

    void Update()
    {
        if (phase == Phase.Inactive)
            return;

        if (phase == Phase.Complete)
        {
            Face(teacher.transform, student.transform.position);
            return;
        }

        if (inDialogue)
        {
            if (conversationPartner != null)
                Face(conversationPartner.transform, player.transform.position);
            return;
        }

        if (phase == Phase.LeadTeacher)
        {
            UpdateEscort();
            return;
        }

        if (Keyboard.current?.fKey.wasPressedThisFrame == true ||
            Gamepad.current?.buttonWest.wasPressedThisFrame == true)
        {
            SchoolNpc nearbyPerson = FindNearbyPerson(TalkRadius);
            if (nearbyPerson != null)
                TalkTo(nearbyPerson);
        }
    }

    void LateUpdate()
    {
        if (phase == Phase.Inactive || inDialogue)
            return;

        Vector3 studentPosition = student.transform.position + Vector3.up * 1.25f;
        if (phase == Phase.LeadTeacher || phase == Phase.Complete)
        {
            bool atBench = DistanceSquared(player.transform.position, student.transform.position) <= TalkRadius * TalkRadius;
            string hint = atBench ? "Wait for the teacher to reach the bench." : "Return to the bench by the front gate.";
            if (phase == Phase.Complete)
                ui.ShowObjective("The teacher found the student.", "Your report helped them get here.");
            else
                ui.ShowObjective("Lead the teacher back.", hint);
            ui.ShowWaypoint(viewCamera, studentPosition, "Front gate bench");
            return;
        }

        SchoolNpc nearbyPerson = FindNearbyPerson(TalkRadius);
        SchoolNpc visiblePerson = FindNearbyPerson(NameRadius);
        if (phase == Phase.CheckStudent)
        {
            ui.ShowObjective("Check on the student.", nearbyPerson != null ?
                "Press F or gamepad X to talk." : "Someone by the front gate is asking for help.");
            ui.ShowWaypoint(viewCamera, visiblePerson != null ? LabelPosition(visiblePerson) : studentPosition,
                visiblePerson != null ? RoleName(visiblePerson) : "Student needs help");
        }
        else if (phase == Phase.FindTeacher)
        {
            ui.ShowObjective("Find a teacher who can help.", nearbyPerson != null ?
                "Press F or gamepad X to talk." : "Look near the school office.");
            ui.ShowWaypoint(visiblePerson != null ? viewCamera : null,
                visiblePerson != null ? LabelPosition(visiblePerson) : Vector3.zero,
                visiblePerson != null ? RoleName(visiblePerson) : string.Empty);
        }
    }

    SchoolNpc FindNearbyPerson(float range)
    {
        Vector3 position = player.transform.position;
        float maxDistance = range * range;
        float studentDistance = DistanceSquared(position, student.transform.position);
        float teacherDistance = DistanceSquared(position, teacher.transform.position);
        float classmateDistance = DistanceSquared(position, classmate.transform.position);

        if (studentDistance <= maxDistance && studentDistance <= teacherDistance && studentDistance <= classmateDistance)
            return student;
        if (teacherDistance <= maxDistance && teacherDistance <= classmateDistance)
            return teacher;
        return classmateDistance <= maxDistance ? classmate : null;
    }

    Vector3 LabelPosition(SchoolNpc person) => person.transform.position +
        Vector3.up * (person == teacher ? 1.95f : person == student ? 1.25f : 1.6f);

    string RoleName(SchoolNpc person) => person == teacher ? "Teacher on duty" :
        person == student ? "Student by the bench" : "Classmate";

    void TalkTo(SchoolNpc person)
    {
        if (person == student)
        {
            ShowDialogue("Student by the bench",
                "I tripped and fell beside this bench by the front gate. Can you ask a teacher to come?",
                new[] { "I'll find the teacher on duty." },
                _ =>
                {
                    student.SetCalling(false);
                    LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_CheckStudent, LearningEventType.Completed));
                    phase = Phase.FindTeacher;
                    CloseDialogue();
                    GameAudioManager.EnsureInstance().PlayVoiceLine("Player", "L2_PLAYER_001");
                });
            return;
        }

        conversationPartner = person;
        if (person == classmate)
        {
            ShowDialogue("Classmate", "The teacher on duty is by the school office.",
                new[] { "Thanks. I'll look by the office." }, _ => CloseDialogue());
            return;
        }

        if (phase == Phase.CheckStudent)
        {
            ShowDialogue("Teacher on duty", "I can help. Can you show me who needs me?",
                new[] { "Someone near the front gate called out. I'll go check." }, _ => CloseDialogue());
            return;
        }

        AskIncident();
    }

    void AskIncident()
    {
        phase = Phase.ReportIncident;
        ShowDialogue("Teacher on duty", "What happened to the student?", IncidentAnswers, answer =>
        {
            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportIncident, LearningEventType.Attempted));
            if (answer != 1)
            {
                mistakes++;
                LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportIncident, LearningEventType.Mistake));
                LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportIncident, LearningEventType.HintShown));
                ui.ShowFeedback(AdaptiveLearningManager.HintFor(LearningObjectiveId.School_ReportIncident,
                    "Think about what the student said by the bench."), false);
                return;
            }

            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportIncident, LearningEventType.Completed));
            ui.ShowFeedback("Now I know what happened.", true);
            StartCoroutine(ContinueDialogue(AskLocation));
        });
    }

    void AskLocation()
    {
        phase = Phase.ReportLocation;
        ShowDialogue("Teacher on duty", "Where did they fall?", LocationAnswers, answer =>
        {
            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportLocation, LearningEventType.Attempted));
            if (answer != 2)
            {
                mistakes++;
                LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportLocation, LearningEventType.Mistake));
                LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportLocation, LearningEventType.HintShown));
                ui.ShowFeedback(AdaptiveLearningManager.HintFor(LearningObjectiveId.School_ReportLocation,
                    "Name the landmark beside the student so I know where to go."), false);
                return;
            }

            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_ReportLocation, LearningEventType.Completed));
            ui.ShowFeedback("The bench by the front gate. I’ll come with you.", true);
            StartCoroutine(ContinueDialogue(StartEscort));
        });
    }

    IEnumerator ContinueDialogue(Action next)
    {
        yield return new WaitForSecondsRealtime(0.7f);
        next();
    }

    void ShowDialogue(string heading, string prompt, string[] choices, Action<int> onAnswer)
    {
        inDialogue = true;
        player.enabled = false;
        GameAudioManager.EnsureInstance().PlayDialogueFor(heading, prompt);
        ui.ShowDialogue(heading, prompt, choices, onAnswer);
    }

    void CloseDialogue()
    {
        inDialogue = false;
        conversationPartner = null;
        GameAudioManager.EnsureInstance().StopVoice();
        ui.HideModal();
        player.enabled = true;
    }

    void StartEscort()
    {
        phase = Phase.LeadTeacher;
        LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_FindTeacher, LearningEventType.Completed));
        trail.Clear();
        lastTrailPoint = player.transform.position;
        trail.Enqueue(lastTrailPoint);
        CloseDialogue();
    }

    void UpdateEscort()
    {
        Vector3 playerPosition = player.transform.position;
        Vector3 teacherPosition = teacher.transform.position;
        Vector3 studentPosition = student.transform.position;

        // Follow the player's recent route around props instead of cutting across the yard.
        if (DistanceSquared(playerPosition, lastTrailPoint) >= TrailSpacing * TrailSpacing)
        {
            lastTrailPoint = playerPosition;
            trail.Enqueue(playerPosition);
        }

        bool playerAtBench = DistanceSquared(playerPosition, studentPosition) <= TalkRadius * TalkRadius;
        while (trail.Count > 0 && DistanceSquared(teacherPosition, trail.Peek()) <= 0.12f * 0.12f)
            trail.Dequeue();

        bool reachingBench = playerAtBench && trail.Count <= 1;
        if (!playerAtBench)
            teacherArrivalSide = 0f;
        else if (teacherArrivalSide == 0f)
            teacherArrivalSide = playerPosition.z >= studentPosition.z ? -1f : 1f;

        Vector3 teacherGoal = studentPosition + new Vector3(0.9f, 0f, teacherArrivalSide);
        Vector3 target = reachingBench ? teacherGoal : trail.Count > 0 ? trail.Peek() : playerPosition;
        target.y = teacherPosition.y;

        float remaining = Vector3.Distance(teacherPosition, target);
        bool following = !playerAtBench && trail.Count <= 1;
        float allowedTravel = following
            ? Mathf.Min(remaining, Mathf.Max(0f, Mathf.Sqrt(DistanceSquared(teacherPosition, playerPosition)) - TeacherFollowingDistance))
            : remaining;
        float desiredSpeed = following || reachingBench
            ? Mathf.Min(TeacherSpeed, Mathf.Sqrt(2f * TeacherAcceleration * allowedTravel))
            : TeacherSpeed;
        escortSpeed = Mathf.MoveTowards(escortSpeed, desiredSpeed, TeacherAcceleration * Time.deltaTime);
        float travel = Mathf.Min(allowedTravel, escortSpeed * Time.deltaTime);
        Vector3 motion = Vector3.MoveTowards(teacherPosition, target, travel);
        float motionSpeed = Time.deltaTime > 0f ? travel / Time.deltaTime : 0f;
        bool walking = motionSpeed > 0.08f;
        teacher.SetWalking(walking, Mathf.Lerp(0.68f, 1.45f, motionSpeed / TeacherSpeed));
        if (walking)
            Face(teacher.transform, target);
        teacher.transform.position = motion;

        if (playerAtBench && DistanceSquared(motion, teacherGoal) <= 0.15f * 0.15f)
        {
            teacher.SetWalking(false);
            player.enabled = false;
            phase = Phase.Complete;
            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.School_LeadTeacher, LearningEventType.Completed));
            GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L2_NAR_006");
            int stars = LevelProgress.StarsForMistakes(mistakes);
            StartCoroutine(ContinueDialogue(() => completed?.Invoke(stars)));
        }
    }

    static void Face(Transform person, Vector3 position)
    {
        Vector3 heading = position - person.position;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.0001f)
            person.rotation = Quaternion.RotateTowards(person.rotation, Quaternion.LookRotation(heading),
                TeacherTurnSpeed * Time.deltaTime);
    }

    static float DistanceSquared(Vector3 first, Vector3 second)
    {
        Vector3 difference = first - second;
        difference.y = 0f;
        return difference.sqrMagnitude;
    }
}
