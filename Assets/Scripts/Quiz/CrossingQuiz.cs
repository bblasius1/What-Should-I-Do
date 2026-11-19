using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the sidewalk approach, safety quiz, and zebra crossing in one replayable round.</summary>
[DefaultExecutionOrder(100)]
public class CrossingQuiz : MonoBehaviour
{
    [System.Serializable]
    public class Question
    {
        public string prompt;
        public string[] answers = new string[3];
        public int correctIndex;
        public string hint;
    }

    enum Stage { Welcome, Approach, Quiz, Crossing, Complete, School, SchoolComplete, Playground, PlaygroundComplete }
    enum CurbLook { Left = -1, None, Right }

    const float CurbClearance = 0.15f;
    const float CurbTolerance = 0.03f;
    const float CurbLookRange = 0.7f;
    const float LookCheckSeconds = 0.4f;
    const float StripeTolerance = 0.2f;
    const float CrossingApproachDistance = 3f;
    const float SidewalkOuterEdge = 7.85f;
    const float StreetEnd = 29.5f;
    const string MoveHint = "Move with WASD, arrow keys, or a gamepad.";

    [Header("Where the quiz opens on the near sidewalk")]
    public Vector3 gateCenter = new Vector3(6.3f, 0.2f, 12.5f);
    [Min(0.1f)] public float gateRadius = 2.5f;

    [Header("Crossing and far sidewalk")]
    [Min(0.1f)] public float roadHalfWidth = 5f;
    public float farSideX = -5.2f;
    public float crossingZMin = 13f;
    public float crossingZMax = 19f;

    public Question[] questions = BuildDefaultQuestions();

    PlayerWalker player;
    ThirdPersonFollowCamera followCamera;
    Camera viewCamera;
    QuizUi ui;
    WelcomeMenu welcome;
    StreetTraffic traffic;
    SchoolHelpStage school;
    PlaygroundStage playground;
    Vector3 startPosition;
    Quaternion startRotation;
    Stage stage;
    int questionIndex;
    int crossingMistakes;
    bool crossingAttemptScored;
    bool transitioning;
    string routeReminder;
    float reminderUntil;
    bool practiceStarted;
    bool checkedLeft;
    bool checkedRight;
    CurbLook curbLook;
    float lookHeldSeconds;

    float NearCurbX => roadHalfWidth + CurbClearance;
    internal float StripeStart => crossingZMin - StripeTolerance;
    internal float StripeEnd => crossingZMax + StripeTolerance;
    float StripeCenter => (crossingZMin + crossingZMax) * 0.5f;

    internal bool IsTrafficYieldRequested
    {
        get
        {
            if (player == null || stage != Stage.Crossing || !practiceStarted)
                return false;

            Vector3 position = player.transform.position;
            return position.x >= farSideX && position.z >= StripeStart - CrossingApproachDistance &&
                position.z <= StripeEnd + CrossingApproachDistance;
        }
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerWalker>();
        if (player == null)
        {
            Debug.LogError("[CrossingQuiz] No PlayerWalker in the scene.");
            enabled = false;
            return;
        }

        if (!HasPlayableQuestions())
        {
            Debug.LogWarning("[CrossingQuiz] The configured quiz has invalid questions. Using the default questions.");
            questions = BuildDefaultQuestions();
        }

        GameAudioManager.EnsureInstance();
        startPosition = player.transform.position;
        startRotation = player.transform.rotation;
        player.CrossingRules = this;
        followCamera = FindAnyObjectByType<ThirdPersonFollowCamera>();
        viewCamera = Camera.main;
        ui = QuizUi.Create(transform);
        welcome = WelcomeMenu.Create(transform, player, followCamera, RequestLevel);
        var road = GameObject.Find("MainRoad");
        if (road != null && road.TryGetComponent(out Renderer surface))
        {
            traffic = gameObject.AddComponent<StreetTraffic>();
            traffic.Initialize(this, surface.bounds);
        }
        ShowWelcome();
    }

    void Update()
    {
        switch (stage)
        {
            case Stage.Approach:
                Vector3 offset = player.transform.position - gateCenter;
                offset.y = 0f;
                if (offset.sqrMagnitude <= gateRadius * gateRadius)
                {
                    stage = Stage.Quiz;
                    player.enabled = false;
                    ShowCurrentQuestion();
                }
                break;
            case Stage.Crossing:
                Vector3 position = player.transform.position;
                if (position.x <= farSideX && IsOnCrossing(position.z))
                    Win();
                else
                    UpdateCurbLook(position);
                break;
        }
    }

    void LateUpdate()
    {
        if (stage != Stage.Approach && stage != Stage.Crossing)
            return;

        Vector3 position = player.transform.position;
        bool approaching = stage == Stage.Approach;
        bool atStripes = IsOnCrossing(position.z);
        if (!approaching && IsAtCurb(position))
        {
            ShowCurbObjective();
            ui.ShowWaypoint(null, default, string.Empty);
            return;
        }

        string objective = approaching
            ? "Walk to the zebra crossing."
            : "Walk to the white stripes.";
        if (!approaching && atStripes)
            objective = position.x >= NearCurbX ? "Walk up to the curb." : "Walk across the white stripes.";
        string hint = Time.unscaledTime < reminderUntil ? routeReminder : MoveHint;
        ui.ShowObjective(objective, hint);

        bool showFarSide = !approaching && atStripes;
        Vector3 waypoint = new Vector3(showFarSide ? farSideX - 0.9f : gateCenter.x, gateCenter.y + 0.8f, StripeCenter);
        ui.ShowWaypoint(viewCamera, waypoint, showFarSide ? "Other sidewalk" : "Zebra crossing");
    }

    internal Vector3 ConstrainMovement(Vector3 position, Vector3 step)
    {
        if (stage == Stage.School)
            return school.ConstrainMovement(position, step);
        if (stage == Stage.Playground)
            return playground.ConstrainMovement(position, step);

        if (step.x >= -0.0001f)
            crossingAttemptScored = false;

        Vector3 destination = position + step;
        destination.x = Mathf.Clamp(destination.x, -SidewalkOuterEdge, SidewalkOuterEdge);
        destination.z = Mathf.Clamp(destination.z, -StreetEnd, StreetEnd);

        if (stage == Stage.Crossing && IsAtCurb(position) && ReadCurbLook() != CurbLook.None)
            return new Vector3(0f, step.y, 0f);

        if (destination.x >= NearCurbX)
            return destination - position;

        if (stage == Stage.Approach)
        {
            destination.x = NearCurbX;
            ScoreBlockedCrossing();
            Remind("Stay on the sidewalk and head to the zebra crossing.");
        }
        else if (stage == Stage.Crossing && !IsOnCrossing(destination.z))
        {
            if (position.x >= NearCurbX)
            {
                destination.x = NearCurbX;
                ScoreBlockedCrossing();
            }
            else
                destination.z = Mathf.Clamp(destination.z, StripeStart, StripeEnd);

            Remind("Use the white stripes to cross the road.");
        }
        else if (stage == Stage.Crossing && position.x >= NearCurbX - CurbTolerance)
        {
            BeginCurbPractice();
            if (!checkedLeft || !checkedRight || (traffic != null && !traffic.CanEnterCrossing()))
            {
                destination.x = NearCurbX;
                ScoreBlockedCrossing();
            }
        }

        return destination - position;
    }

    void ScoreBlockedCrossing()
    {
        if (crossingAttemptScored)
            return;

        crossingAttemptScored = true;
        crossingMistakes++;
        LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Crossing_WaitForTraffic, LearningEventType.Mistake));
    }

    bool IsOnCrossing(float z) => z >= StripeStart && z <= StripeEnd;

    bool IsAtCurb(Vector3 position) => IsOnCrossing(position.z) &&
        position.x >= NearCurbX - CurbTolerance && position.x <= NearCurbX + CurbLookRange;

    static CurbLook ReadCurbLook()
    {
        var keyboard = Keyboard.current;
        var gamepad = Gamepad.current;
        bool left = keyboard?.qKey.isPressed == true || gamepad?.leftShoulder.isPressed == true;
        bool right = keyboard?.eKey.isPressed == true || gamepad?.rightShoulder.isPressed == true;
        if (left == right)
            return CurbLook.None;

        return left ? CurbLook.Left : CurbLook.Right;
    }

    void BeginCurbPractice()
    {
        if (practiceStarted)
            return;

        practiceStarted = true;
        traffic?.BeginCrossingPractice();
    }

    void UpdateCurbLook(Vector3 position)
    {
        if (!IsAtCurb(position))
        {
            ResetLookChecks();
            return;
        }

        BeginCurbPractice();
        CurbLook look = ReadCurbLook();
        if (look != curbLook)
        {
            curbLook = look;
            lookHeldSeconds = 0f;
            followCamera?.SetCurbLook((int)look);
        }

        if (look == CurbLook.None)
            return;

        lookHeldSeconds += Time.unscaledDeltaTime;
        if (lookHeldSeconds < LookCheckSeconds)
            return;

        if (look == CurbLook.Left)
            checkedLeft = true;
        else
            checkedRight = true;

        if (checkedLeft && checkedRight && !LearningTracker.IsCompleted(LearningObjectiveId.Crossing_LookBothWays))
            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Crossing_LookBothWays, LearningEventType.Completed));
    }

    CurbLook BlockingTraffic()
    {
        if (traffic == null)
            return CurbLook.None;
        if (traffic.IsApproachBlocked(false))
            return CurbLook.Right;
        return traffic.IsApproachBlocked(true) ? CurbLook.Left : CurbLook.None;
    }

    void ShowCurbObjective()
    {
        if (curbLook != CurbLook.None)
        {
            bool left = curbLook == CurbLook.Left;
            bool movingTraffic = traffic != null && traffic.IsApproachBlocked(left);
            string observation = movingTraffic ? "Traffic is still moving. Wait at the curb." :
                "Traffic on this side is clear or stopped.";
            ui.ShowObjective(left ? "Looking left" : "Looking right", observation);
        }
        else if (!checkedLeft && !checkedRight)
        {
            ui.ShowObjective("Look both ways.", "Hold Q or LB to look left. Hold E or RB to look right.");
        }
        else if (!checkedLeft || !checkedRight)
        {
            if (!checkedLeft)
                ui.ShowObjective("Now look left.", "Hold Q or LB and check for moving traffic.");
            else
                ui.ShowObjective("Now look right.", "Hold E or RB and check for moving traffic.");
        }
        else
        {
            CurbLook blocker = BlockingTraffic();
            if (blocker == CurbLook.None)
                ui.ShowObjective("Cross on the white stripes.", "Keep watching for traffic as you cross.");
            else
                ui.ShowObjective("Wait at the curb.", blocker == CurbLook.Left ?
                    "A vehicle from the left is still moving." : "A vehicle from the right is still moving.");
        }
    }

    void ResetLookChecks()
    {
        checkedLeft = false;
        checkedRight = false;
        lookHeldSeconds = 0f;
        if (curbLook == CurbLook.None)
            return;

        curbLook = CurbLook.None;
        followCamera?.SetCurbLook(0);
    }

    void Remind(string message)
    {
        routeReminder = message;
        reminderUntil = Time.unscaledTime + 2f;
    }

    void ShowCurrentQuestion()
    {
        var question = questions[questionIndex];
        ui.ShowQuestion(questionIndex + 1, questions.Length, question.prompt, question.answers, OnAnswer);
    }

    void OnAnswer(int choice)
    {
        var question = questions[questionIndex];
        if (choice != question.correctIndex)
        {
            crossingMistakes++;
            LearningObjectiveId objective = QuestionObjective(questionIndex);
            LearningTracker.RecordEvent(new LearningEvent(objective, LearningEventType.Mistake));
            ui.ShowFeedback(AdaptiveLearningManager.HintFor(objective,
                string.IsNullOrWhiteSpace(question.hint) ? "Try another answer." : question.hint), false);
            return;
        }

        LearningTracker.RecordEvent(new LearningEvent(QuestionObjective(questionIndex), LearningEventType.Completed));
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L1_NAR_011");
        ui.ShowFeedback("That's right!", true);
        StartCoroutine(AdvanceAfterDelay());
    }

    static LearningObjectiveId QuestionObjective(int index) => index switch
    {
        0 => LearningObjectiveId.Crossing_UseZebra,
        1 => LearningObjectiveId.Crossing_LookBothWays,
        _ => LearningObjectiveId.Crossing_WaitForTraffic,
    };

    IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSecondsRealtime(0.9f);

        questionIndex++;
        if (questionIndex < questions.Length)
        {
            ShowCurrentQuestion();
            yield break;
        }

        stage = Stage.Crossing;
        Remind("Look both ways and listen before stepping out.");
        ui.HideModal();
        player.enabled = true;
    }

    void Win()
    {
        stage = Stage.Complete;
        ResetLookChecks();
        LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Crossing_UseZebra, LearningEventType.Completed));
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L1_NAR_012");
        ShowLevelResult(GameLevel.Crossing, LevelProgress.StarsForMistakes(crossingMistakes));
    }

    void RequestLevel(GameLevel level)
    {
        if (LevelProgress.Best(level) == 0 && LevelProgress.FirstUnfinished() != level)
            return;

        TransitionTo(() => EnterLevel(level));
    }

    void EnterLevel(GameLevel level)
    {
        school?.Stop();
        playground?.Stop();
        welcome.HideForPlay();
        traffic?.SetTrafficAudioEnabled(level == GameLevel.Crossing);
        GameAudioManager.EnsureInstance().StopVoice();
        GameAudioManager.EnsureInstance().PlayMusic(level switch
        {
            GameLevel.Crossing => GameMusicTrack.Street,
            GameLevel.School => GameMusicTrack.School,
            _ => GameMusicTrack.Playground,
        });
        ResetLookChecks();
        ui.SetLevelHeading(level);

        switch (level)
        {
            case GameLevel.Crossing:
                Restart();
                break;
            case GameLevel.School:
                stage = Stage.School;
                if (school == null)
                    school = gameObject.AddComponent<SchoolHelpStage>();
                school.Begin(player, ui, viewCamera, CompleteSchool);
                break;
            case GameLevel.Playground:
                stage = Stage.Playground;
                if (playground == null)
                    playground = gameObject.AddComponent<PlaygroundStage>();
                playground.Begin(player, ui, viewCamera, CompletePlayground);
                break;
        }

        followCamera?.SetLevelView(level);
    }

    void CompleteSchool(int stars)
    {
        if (stage != Stage.School)
            return;

        stage = Stage.SchoolComplete;
        ShowLevelResult(GameLevel.School, stars);
    }

    void CompletePlayground(int stars)
    {
        if (stage != Stage.Playground)
            return;

        stage = Stage.PlaygroundComplete;
        ShowLevelResult(GameLevel.Playground, stars);
    }

    void ShowLevelResult(GameLevel level, int stars)
    {
        player.enabled = false;
        int best = LevelProgress.Record(level, stars);
        Action next = level switch
        {
            GameLevel.Crossing => () => RequestLevel(GameLevel.School),
            GameLevel.School => () => RequestLevel(GameLevel.Playground),
            _ => null,
        };
        ui.ShowLevelResult(level, stars, best, () => RequestLevel(level), () => TransitionTo(ShowWelcome), next);
    }

    void TransitionTo(Action action)
    {
        if (transitioning)
            return;

        transitioning = true;
        StartCoroutine(TransitionRoutine(action));
    }

    IEnumerator TransitionRoutine(Action action)
    {
        yield return FadeScreen(0f, 1f, 0.18f);
        action();
        yield return null;
        yield return FadeScreen(1f, 0f, 0.28f);
        transitioning = false;
    }

    IEnumerator FadeScreen(float start, float end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            ui.SetTransitionOpacity(Mathf.SmoothStep(start, end, elapsed / duration));
            yield return null;
        }
        ui.SetTransitionOpacity(end);
    }

    void ShowWelcome()
    {
        stage = Stage.Welcome;
        traffic?.SetTrafficAudioEnabled(false);
        school?.Stop();
        playground?.Stop();
        followCamera?.SetLevelView(GameLevel.Crossing);
        ui.SetLevelHeading(GameLevel.Crossing);
        ResetLookChecks();
        player.enabled = false;
        player.ResetTo(startPosition, startRotation);
        GameAudioManager.EnsureInstance().StopVoice();
        ui.HideModal();
        ui.SetVisible(false);
        welcome.Show();
        GameAudioManager.EnsureInstance().PlayMusic(GameMusicTrack.MainMenu);

        if (viewCamera != null)
        {
            Vector3 towardCamera = viewCamera.transform.position - player.transform.position;
            towardCamera.y = 0f;
            if (towardCamera.sqrMagnitude > 0.001f)
                player.transform.rotation = Quaternion.LookRotation(towardCamera);
        }
    }

    void Restart()
    {
        practiceStarted = false;
        crossingMistakes = 0;
        crossingAttemptScored = false;
        GameAudioManager.EnsureInstance().StopVoice();
        player.ResetTo(startPosition, startRotation);
        questionIndex = 0;
        reminderUntil = 0f;
        stage = Stage.Approach;
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L1_NAR_001");
        ui.SetVisible(true);
        ui.HideModal();
        player.enabled = true;
    }

    bool HasPlayableQuestions()
    {
        if (questions == null || questions.Length == 0)
            return false;

        foreach (var question in questions)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.prompt) ||
                question.answers == null || question.answers.Length < 2 ||
                question.answers.Length > QuizUi.MaxAnswers ||
                question.correctIndex < 0 || question.correctIndex >= question.answers.Length)
                return false;

            foreach (var answer in question.answers)
                if (string.IsNullOrWhiteSpace(answer))
                    return false;
        }

        return true;
    }

    void OnDestroy()
    {
        if (player != null && player.CrossingRules == this)
            player.CrossingRules = null;
    }

    static Question[] BuildDefaultQuestions()
    {
        return new[]
        {
            new Question
            {
                prompt = "You want to get to school on the other side. Where should you cross?",
                answers = new[]
                {
                    "Anywhere the road looks empty",
                    "On the white stripes of the zebra crossing",
                    "Between the parked cars",
                },
                correctIndex = 1,
                hint = "Cars can come fast. Look for the white stripes.",
            },
            new Question
            {
                prompt = "You are standing at the zebra crossing. What do you do first?",
                answers = new[]
                {
                    "Start running across",
                    "Look left and right, and listen",
                    "Look at my phone",
                },
                correctIndex = 1,
                hint = "Before you step out, check the road both ways.",
            },
            new Question
            {
                prompt = "A car is coming, so you wait on the sidewalk. When is it safe to cross?",
                answers = new[]
                {
                    "When the car has really stopped for you",
                    "When I have counted to three",
                    "When my friend crosses first",
                },
                correctIndex = 0,
                hint = "Stay on the sidewalk until the car has stopped.",
            },
        };
    }
}
