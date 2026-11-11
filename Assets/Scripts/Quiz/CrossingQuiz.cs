using System.Collections;
using UnityEngine;

/// <summary>
/// The game: walking up to the zebra crossing starts a short safety quiz, every answer must be
/// right before the crossing opens, and reaching the far sidewalk wins the round.
/// </summary>
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

    [Header("Where the questions open, just before the zebra crossing")]
    public Vector3 gateCenter = new Vector3(6.3f, 0.2f, 12.5f);
    public float gateRadius = 2.5f;

    [Header("Win when the player reaches the far sidewalk across the crossing")]
    public float farSideX = -5.2f;
    public float crossingZMin = 12f;
    public float crossingZMax = 20f;

    public Question[] questions = BuildDefaultQuestions();

    PlayerWalker player;
    QuizUi ui;
    Vector3 startPosition;
    Quaternion startRotation;
    int index;
    bool gateCleared;
    bool busy;
    bool resolving;
    bool finished;

    void Start()
    {
        player = FindAnyObjectByType<PlayerWalker>();
        if (player == null)
        {
            Debug.LogError("[CrossingQuiz] No PlayerWalker in the scene, the quiz is disabled.");
            enabled = false;
            return;
        }

        startPosition = player.transform.position;
        startRotation = player.transform.rotation;

        ui = QuizUi.Create(transform);
    }

    void Update()
    {
        if (player == null || busy)
            return;

        Vector3 position = player.transform.position;

        if (!gateCleared)
        {
            Vector3 offset = position - gateCenter;
            offset.y = 0f;

            if (offset.sqrMagnitude <= gateRadius * gateRadius)
                AskQuestions();
            return;
        }

        if (!finished && position.x < farSideX && position.z > crossingZMin && position.z < crossingZMax)
            Win();
    }

    void AskQuestions()
    {
        busy = true;
        index = 0;
        player.enabled = false;
        ShowCurrentQuestion();
    }

    void ShowCurrentQuestion()
    {
        resolving = false;
        var question = questions[index];
        ui.ShowQuestion(question.prompt, question.answers, OnAnswer);
    }

    void OnAnswer(int choice)
    {
        if (resolving)
            return;

        var question = questions[index];
        if (choice != question.correctIndex)
        {
            ui.ShowFeedback(question.hint, false);
            return;
        }

        resolving = true;
        ui.ShowFeedback("That's right!", true);
        StartCoroutine(AdvanceAfterDelay());
    }

    IEnumerator AdvanceAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        index++;
        if (index < questions.Length)
        {
            ShowCurrentQuestion();
            yield break;
        }

        StartCoroutine(OpenCrossing());
    }

    IEnumerator OpenCrossing()
    {
        ui.ShowGo("Great! Look both ways, then cross on the white stripes.");
        yield return new WaitForSeconds(1.8f);

        ui.Hide();
        gateCleared = true;
        busy = false;
        player.enabled = true;
    }

    void Win()
    {
        finished = true;
        busy = true;
        player.enabled = false;
        ui.ShowWin("You made it to school safely!\nWell done.", Restart);
    }

    void Restart()
    {
        ui.HideWin();

        player.transform.SetPositionAndRotation(startPosition, startRotation);
        player.enabled = true;

        var follow = FindAnyObjectByType<ThirdPersonFollowCamera>();
        if (follow != null)
            follow.SnapToTarget();

        index = 0;
        gateCleared = false;
        resolving = false;
        busy = false;
        finished = false;
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
                hint = "Not quite. Cars can come fast, so look for the white stripes.",
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
                hint = "Close! Before you step out, check the road both ways.",
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
                hint = "Let's stay safe and wait until the car has stopped.",
            },
        };
    }
}
