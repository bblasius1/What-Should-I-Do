using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Runs the no-timer inclusion and turn-taking lesson.</summary>
[DisallowMultipleComponent]
public sealed class PlaygroundStage : MonoBehaviour
{
    enum Phase { Inactive, Invite, Joining, MakeRoom, Pass, Complete }

    const int PlaymateCount = 3;
    const float InviteRadius = 1.85f;
    const float StartCircleRadius = 0.67f;
    const float PassRadius = 9f;
    const float AimThreshold = 0.48f;
    const float BoundaryPadding = 0.31f;
    const float StepSpeed = 2.75f;

    readonly PlaygroundChild[] playmates = new PlaygroundChild[PlaymateCount];
    readonly PlaygroundLayout.PlaygroundMarker[] pads = new PlaygroundLayout.PlaygroundMarker[PlaymateCount];
    readonly bool[] served = new bool[PlaymateCount];

    PlaygroundLayout world;
    PlaygroundLayout.PlaygroundMarker openPad;
    PlaygroundLayout.PlaygroundMarker invitePad;
    Transform ball;
    AudioSource catchSound;
    AudioClip catchClip;
    PlayerWalker player;
    QuizUi ui;
    Camera viewCamera;
    Action<int> completed;
    Phase phase;
    int focusedPlayer;
    int turns;
    int mistakes;
    bool ballInFlight;
    float coachingUntil;
    float positiveUntil;

    internal void Begin(PlayerWalker walker, QuizUi quizUi, Camera camera, Action<int> onComplete)
    {
        Stop();

        player = walker;
        ui = quizUi;
        viewCamera = camera;
        completed = onComplete;
        world = PlaygroundLayout.Create(transform);
        ball = world.CreateBall();
        catchSound = ball.gameObject.AddComponent<AudioSource>();
        catchSound.playOnAwake = false;
        catchSound.volume = 0.45f * GameAudioManager.GetSfxVolume();
        catchClip = MakeCatchClip();

        playmates[0] = PlaygroundChild.Create(world.transform, "Classmate waiting for a turn", "Playground/PlaygroundBuddy",
            world.InvitationPoint, new Color32(126, 91, 161, 255), 1.34f);
        playmates[1] = PlaygroundChild.Create(world.transform, "Classmate in green", "School/Classmate",
            world.CatchSpot(1), new Color32(109, 165, 121, 255), 1.30f);
        playmates[2] = PlaygroundChild.Create(world.transform, "Classmate in yellow", "School/Student",
            world.CatchSpot(2), new Color32(234, 178, 61, 255), 1.27f);
        for (int index = 0; index < PlaymateCount; index++)
        {
            pads[index] = world.AddMarker("Classmate spot", world.CatchSpot(index));
            FacePoint(playmates[index].transform, world.OpenSpot, 1f);
        }
        pads[0].Show(false);

        invitePad = world.AddMarker("Waiting classmate", world.InvitationPoint);
        invitePad.Set(false, true);
        openPad = world.AddMarker("Open spot", world.OpenSpot);
        openPad.Show(false);
        player.ResetTo(world.StartPoint, Quaternion.identity);
        ui.SetVisible(true);
        ui.HideModal();
        player.enabled = true;
        phase = Phase.Invite;
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L3_NAR_001");
        UpdateBallAtPlayer();
    }

    internal void Stop()
    {
        StopAllCoroutines();
        phase = Phase.Inactive;
        completed = null;
        turns = 0;
        mistakes = 0;
        focusedPlayer = -1;
        ballInFlight = false;
        coachingUntil = 0f;
        positiveUntil = 0f;
        Array.Clear(served, 0, served.Length);
        if (catchClip != null)
        {
            Destroy(catchClip);
            catchClip = null;
        }
        if (world != null)
        {
            world.gameObject.SetActive(false);
            Destroy(world.gameObject);
            world = null;
        }
    }

    internal Vector3 ConstrainMovement(Vector3 position, Vector3 step)
    {
        if (world == null)
            return step;

        Vector3 destination = position + step;
        Bounds bounds = world.WalkableBounds;
        destination.x = Mathf.Clamp(destination.x, bounds.min.x + BoundaryPadding, bounds.max.x - BoundaryPadding);
        destination.z = Mathf.Clamp(destination.z, bounds.min.z + BoundaryPadding, bounds.max.z - BoundaryPadding);
        return destination - position;
    }

    void Update()
    {
        if (phase == Phase.Inactive || phase == Phase.Complete)
            return;

        Vector3 position = player.transform.position;
        bool actionPressed = Keyboard.current?.fKey.wasPressedThisFrame == true ||
            Gamepad.current?.buttonWest.wasPressedThisFrame == true;
        if (phase == Phase.Invite)
        {
            if (InRange(position, world.InvitationPoint, InviteRadius) && actionPressed)
                StartCoroutine(InviteClassmate());
        }
        else if (phase == Phase.MakeRoom && InRange(position, world.OpenSpot, StartCircleRadius))
        {
            phase = Phase.Pass;
            LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Playground_MakeRoom, LearningEventType.Completed));
            openPad.Set(true, false);
        }
        else if (phase == Phase.Pass && !ballInFlight)
        {
            focusedPlayer = FindReceiver();
            if (focusedPlayer >= 0 && actionPressed)
            {
                if (served[focusedPlayer])
                {
                    mistakes++;
                    LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Playground_ShareTurns, LearningEventType.Mistake));
                    coachingUntil = Time.unscaledTime + 2.6f;
                }
                else
                {
                    coachingUntil = 0f;
                    StartCoroutine(PassBall(focusedPlayer));
                }
            }
        }

        if (!ballInFlight)
            UpdateBallAtPlayer();
    }

    IEnumerator InviteClassmate()
    {
        phase = Phase.Joining;
        LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Playground_Invite, LearningEventType.Completed));
        GameAudioManager.EnsureInstance().PlayVoiceLine("NewFriend", "L3_FRIEND_003");
        invitePad.Show(false);
        openPad.Show(true);
        openPad.Set(false, true);
        PlaygroundChild newcomer = playmates[0];
        Vector3 waypoint = Vector3.Lerp(world.InvitationPoint, world.CatchSpot(0), 0.5f) + Vector3.back * 0.72f;
        newcomer.SetWalking(true);
        yield return MovePlaymate(newcomer, waypoint);
        yield return MovePlaymate(newcomer, world.CatchSpot(0));
        newcomer.SetWalking(false);
        FacePoint(newcomer.transform, world.OpenSpot, 1f);
        pads[0].Show(true);
        phase = Phase.MakeRoom;
    }

    static IEnumerator MovePlaymate(PlaygroundChild child, Vector3 target)
    {
        while ((target - child.transform.position).sqrMagnitude > 0.0004f)
        {
            child.transform.position = Vector3.MoveTowards(child.transform.position, target, StepSpeed * Time.deltaTime);
            FacePoint(child.transform, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            yield return null;
        }
        child.transform.position = target;
    }

    int FindReceiver()
    {
        Vector3 forward = player.transform.forward;
        Vector3 position = player.transform.position;
        int winner = -1;
        float best = AimThreshold;
        for (int index = 0; index < PlaymateCount; index++)
        {
            Vector3 difference = playmates[index].transform.position - position;
            difference.y = 0f;
            float distance = difference.sqrMagnitude;
            if (distance < 0.20f || distance > PassRadius * PassRadius)
                continue;

            float aim = Vector3.Dot(forward, difference.normalized);
            if (aim > best)
            {
                winner = index;
                best = aim;
            }
        }
        return winner;
    }

    IEnumerator PassBall(int receiverIndex)
    {
        ballInFlight = true;
        PlaygroundChild receiver = playmates[receiverIndex];
        receiver.SetReaching(true);
        FacePoint(receiver.transform, player.transform.position, 1f);
        Vector3 start = PlayerHands();
        float distance = Vector3.Distance(start, receiver.Hands);
        yield return FlyBall(start, () => receiver.Hands, Mathf.Clamp(distance * 0.095f, 0.36f, 0.72f));

        served[receiverIndex] = true;
        turns++;
        pads[receiverIndex].Set(true, false);
        positiveUntil = Time.unscaledTime + 1.7f;
        catchSound.volume = 0.45f * GameAudioManager.GetSfxVolume();
        catchSound.PlayOneShot(catchClip);

        float held = 0f;
        while (held < 0.35f)
        {
            ball.position = receiver.Hands;
            held += Time.deltaTime;
            yield return null;
        }

        yield return FlyBall(receiver.Hands, PlayerHands, 0.59f);
        ballInFlight = false;
        receiver.SetReaching(false);
        UpdateBallAtPlayer();

        if (turns != PlaymateCount)
            yield break;

        LearningTracker.RecordEvent(new LearningEvent(LearningObjectiveId.Playground_ShareTurns, LearningEventType.Completed));
        phase = Phase.Complete;
        GameAudioManager.EnsureInstance().PlayVoiceLine("Narrator", "L3_NAR_008");
        for (int index = 0; index < PlaymateCount; index++)
            pads[index].Set(true, false);
        player.enabled = false;
        yield return new WaitForSecondsRealtime(0.55f);
        completed?.Invoke(LevelProgress.StarsForMistakes(mistakes));
    }

    IEnumerator FlyBall(Vector3 start, Func<Vector3> end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 landing = end();
            ball.position = Vector3.Lerp(start, landing, progress) +
                Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 1.05f);
            ball.Rotate(285f * Time.deltaTime, 75f * Time.deltaTime, 105f * Time.deltaTime, Space.Self);
            yield return null;
        }

        ball.position = end();
    }

    void LateUpdate()
    {
        if (phase == Phase.Inactive)
            return;

        Vector3 position = player.transform.position;
        if (phase == Phase.Invite)
        {
            bool nearby = InRange(position, world.InvitationPoint, InviteRadius);
            FacePoint(playmates[0].transform, position, 1f - Mathf.Exp(-8f * Time.deltaTime));
            ui.ShowObjective("Invite the classmate.", nearby ?
                "Press F or gamepad X to invite." : "Walk over to the classmate outside the circle.");
            ui.ShowWaypoint(viewCamera, playmates[0].transform.position + Vector3.up * 1.85f, "Waiting to join");
        }
        else if (phase == Phase.Joining || phase == Phase.MakeRoom)
        {
            ui.ShowObjective("Make room in the circle.", phase == Phase.Joining ?
                "The classmate is joining the game." : "Step onto the open ring to start passing.");
            ui.ShowWaypoint(viewCamera, world.OpenSpot + Vector3.up * 0.40f, "Open spot");
        }
        else if (phase == Phase.Pass)
        {
            TraceTurn();
        }
        else if (phase == Phase.Complete)
        {
            ui.ShowObjective("Everyone got a turn!", "You invited a classmate, made room, and passed the ball to everyone.");
            ui.ShowWaypoint(null, Vector3.zero, string.Empty);
        }
    }

    void TraceTurn()
    {
        for (int index = 0; index < PlaymateCount; index++)
        {
            bool highlighted = index == focusedPlayer;
            pads[index].Set(served[index], highlighted);
            playmates[index].SetReaching(highlighted || !served[index]);
            FacePoint(playmates[index].transform, player.transform.position,
                1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        string objective = GameLanguage.Format("{0} of {1} classmates have had a turn.", turns, PlaymateCount);
        string hint = Time.unscaledTime < coachingUntil
            ? AdaptiveLearningManager.HintFor(LearningObjectiveId.Playground_ShareTurns,
                "Choose someone who is still waiting.") :
            Time.unscaledTime < positiveUntil ? "Nice pass!" : "Face a classmate. Press F or gamepad X to pass.";
        ui.ShowObjective(objective, hint);
        if (focusedPlayer >= 0)
        {
            Vector3 head = playmates[focusedPlayer].transform.position + Vector3.up * 1.86f;
            ui.ShowWaypoint(viewCamera, head, served[focusedPlayer] ? "Already had a turn" : "Ready to catch");
        }
        else
        {
            ui.ShowWaypoint(null, Vector3.zero, string.Empty);
        }
    }

    void UpdateBallAtPlayer()
    {
        if (player != null && ball != null)
            ball.position = PlayerHands();
    }

    Vector3 PlayerHands() => player.transform.position + Vector3.up * 0.96f + player.transform.forward * 0.44f;

    static bool InRange(Vector3 from, Vector3 to, float radius)
    {
        Vector3 difference = from - to;
        difference.y = 0f;
        return difference.sqrMagnitude <= radius * radius;
    }

    static void FacePoint(Transform actor, Vector3 point, float blend)
    {
        Vector3 heading = point - actor.position;
        heading.y = 0f;
        if (heading.sqrMagnitude > 0.00001f)
            actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(heading), blend);
    }

    static AudioClip MakeCatchClip()
    {
        const int rate = 22050;
        var samples = new float[rate / 13];
        for (int i = 0; i < samples.Length; i++)
        {
            float progress = (float)i / samples.Length;
            float radians = progress * (2f * Mathf.PI * 23f - progress * 14f);
            samples[i] = Mathf.Sin(radians) * Mathf.Sin(progress * Mathf.PI) *
                (1f - progress) * (1f - progress) * 0.32f;
        }
        AudioClip clip = AudioClip.Create("Gentle ball catch", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
