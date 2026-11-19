using System.Collections.Generic;
using UnityEngine;

/// <summary>Runs two lanes of traffic and yields at the school's zebra crossing.</summary>
[DisallowMultipleComponent]
public sealed class StreetTraffic : MonoBehaviour
{
    const float StopOffset = 1.2f;
    const float CrossingClearance = 0.4f;
    const float QueueGap = 2.2f;
    const float Acceleration = 2.5f;
    const float Braking = 3.6f;
    const float StoppedSpeed = 0.05f;
    const float ApproachWatchDistance = 14f;
    const float RouteEndFadeDistance = 5f;

    static readonly VehicleKind[] Fleet = { VehicleKind.SmallCar, VehicleKind.MediumCar, VehicleKind.Truck };
    static readonly float[] CruiseSpeeds = { 7.8f, 7.2f, 6.4f };

    readonly List<Lane> lanes = new List<Lane>(2);
    CrossingQuiz crossing;
    AudioClip engineLoop;
    bool trafficAudioEnabled;

    internal void SetTrafficAudioEnabled(bool enabled)
    {
        trafficAudioEnabled = enabled;
        if (enabled)
            return;

        foreach (Lane lane in lanes)
            foreach (Vehicle vehicle in lane.Vehicles)
                vehicle.Engine.Stop();
    }

    internal void Initialize(CrossingQuiz quiz, Bounds road)
    {
        crossing = quiz;
        engineLoop = CreateEngineLoop();
        var group = new GameObject("Street Traffic").transform;
        group.SetParent(transform, false);

        foreach (int direction in new[] { 1, -1 })
        {
            var lane = new Lane(road, direction, crossing.StripeStart, crossing.StripeEnd);
            lanes.Add(lane);

            // Start some traffic within sight from the sidewalks on the first round.
            float lead = lane.CrossingEntrance - (direction > 0 ? 10f : 17f);
            for (int i = 0; i < Fleet.Length; i++)
            {
                int index = (i + (direction > 0 ? 0 : 1)) % Fleet.Length;
                VehicleKind kind = Fleet[index];
                var vehicle = new GameObject(kind.ToString()).transform;
                vehicle.SetParent(group, false);
                VehicleVisuals.Create(vehicle, kind);

                float position = Mathf.Repeat(lead - lane.Length * i / Fleet.Length, lane.Length);
                float halfLength = VehicleVisuals.GetDimensions(kind).z * 0.5f;
                lane.Place(vehicle, position);
                AudioSource engine = AddEngine(vehicle);
                lane.Vehicles.Add(new Vehicle(vehicle, engine, position, halfLength, CruiseSpeeds[index]));
            }
        }

        PlaceBusShelter(road);
    }

    internal void BeginCrossingPractice()
    {
        foreach (Lane lane in lanes)
            lane.PassingVehicle = null;

        Lane passingLane = null;
        Vehicle passingVehicle = null;
        float closest = ApproachWatchDistance;
        foreach (Lane lane in lanes)
        {
            foreach (Vehicle vehicle in lane.Vehicles)
            {
                float distanceToStop = lane.StopLine - vehicle.Front;
                if (distanceToStop < 0f && vehicle.Rear < lane.CrossingExit + CrossingClearance)
                {
                    // A car is already passing the stripes. The exercise can use that encounter.
                    return;
                }

                if (distanceToStop < 0f || distanceToStop >= closest || vehicle.Speed <= StoppedSpeed)
                    continue;

                closest = distanceToStop;
                passingLane = lane;
                passingVehicle = vehicle;
            }
        }

        // Let one approaching car clear naturally instead of repositioning the whole fleet.
        // If neither direction has a nearby arrival, the player can use the available gap.
        if (passingLane != null)
            passingLane.PassingVehicle = passingVehicle;
    }

    void PlaceBusShelter(Bounds road)
    {
        var prefab = Resources.Load<GameObject>("StreetDressing/BusShelter");
        if (prefab == null)
            return;

        var sidewalk = GameObject.Find("LeftSidewalk")?.GetComponent<Renderer>();
        float floorY = sidewalk != null ? sidewalk.bounds.max.y : road.max.y - 0.18f;
        var shelter = new GameObject("Bus shelter").transform;
        shelter.SetParent(transform, false);
        shelter.SetPositionAndRotation(new Vector3(road.min.x - 4.6f, floorY, 2.8f),
            Quaternion.Euler(0f, 90f, 0f));
        if (!SceneModel.TryPlace(shelter, prefab, new Vector3(3.8f, 2.5f, 1.6f), fitWideProp: true))
        {
            Destroy(shelter.gameObject);
            return;
        }

        if (sidewalk == null)
            return;

        float outerEdge = shelter.position.x - 1.2f;
        float innerEdge = sidewalk.bounds.min.x;
        if (innerEdge <= outerEdge)
            return;

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Shelter paving";
        pad.transform.SetParent(transform, false);
        pad.transform.position = new Vector3((outerEdge + innerEdge) * 0.5f, floorY - 0.15f, shelter.position.z);
        pad.transform.localScale = new Vector3(innerEdge - outerEdge, 0.3f, 4.25f);
        pad.GetComponent<Renderer>().sharedMaterial = sidewalk.sharedMaterial;
        pad.GetComponent<Collider>().enabled = false;
    }

    internal bool CanEnterCrossing() => !IsApproachBlocked(true) && !IsApproachBlocked(false);

    internal bool IsApproachBlocked(bool fromLeft)
    {
        foreach (Lane lane in lanes)
        {
            if (lane.FromLeft != fromLeft)
                continue;

            if (lane.PassingVehicle != null)
                return true;

            Vehicle lead = null;
            foreach (Vehicle vehicle in lane.Vehicles)
            {
                if (vehicle.Rear >= lane.CrossingExit + CrossingClearance)
                    continue;

                // A vehicle beyond the stop line is already committed. Give it time to clear.
                if (vehicle.Front > lane.StopLine + 0.01f)
                    return true;

                if (lead == null || vehicle.Front > lead.Front)
                    lead = vehicle;
            }

            // Cars behind a stopped lead driver are still able to roll up to the queue.
            if (lead == null || lead.Speed <= StoppedSpeed)
                return false;

            float distanceToStop = lane.StopLine - lead.Front;
            float brakingDistance = lead.Speed * lead.Speed / (2f * Braking);
            return distanceToStop <= Mathf.Max(ApproachWatchDistance, brakingDistance + 1f);
        }

        return false;
    }

    void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        if (crossing == null || deltaTime <= 0f)
            return;

        bool shouldYield = crossing.IsTrafficYieldRequested;
        foreach (Lane lane in lanes)
        {
            if (!shouldYield || (lane.PassingVehicle != null &&
                lane.PassingVehicle.Rear >= lane.CrossingExit + CrossingClearance))
                lane.PassingVehicle = null;

            List<Vehicle> vehicles = lane.Vehicles;
            vehicles.Sort((a, b) => b.Distance.CompareTo(a.Distance));

            // The first vehicle follows the last one after the route wraps around.
            float nextRear = vehicles[vehicles.Count - 1].Rear + lane.Length;
            foreach (Vehicle vehicle in vehicles)
            {
                float frontLimit = nextRear - QueueGap;
                if (shouldYield && vehicle != lane.PassingVehicle && vehicle.Front <= lane.StopLine + 0.01f)
                    frontLimit = Mathf.Min(frontLimit, lane.StopLine);

                float availableDistance = Mathf.Max(0f, frontLimit - vehicle.Front);
                float targetSpeed = Mathf.Min(vehicle.CruiseSpeed, Mathf.Sqrt(2f * Braking * availableDistance));
                float speedChange = targetSpeed < vehicle.Speed ? Braking : Acceleration;
                vehicle.Speed = Mathf.MoveTowards(vehicle.Speed, targetSpeed, speedChange * deltaTime);
                float travel = vehicle.Speed * deltaTime;
                if (travel >= availableDistance)
                {
                    travel = availableDistance;
                    vehicle.Speed = 0f;
                }

                vehicle.Distance += travel;
                nextRear = vehicle.Rear;
            }

            foreach (Vehicle vehicle in vehicles)
            {
                vehicle.Distance = Mathf.Repeat(vehicle.Distance, lane.Length);
                lane.Place(vehicle.Transform, vehicle.Distance);
                float speedFraction = vehicle.Speed / vehicle.CruiseSpeed;
                vehicle.Engine.pitch = Mathf.Lerp(0.82f, 1.18f, speedFraction) * (vehicle.CruiseSpeed / CruiseSpeeds[0]);
                vehicle.Engine.volume = Mathf.Lerp(0.035f, 0.15f, speedFraction) *
                    vehicle.Transform.localScale.x * GameAudioManager.GetSfxVolume();

                // Traffic objects are created on the initial welcome screen, before
                // browser audio is unlocked. Start their loop after the first click.
                bool audible = trafficAudioEnabled && GameAudioManager.Instance != null &&
                    GameAudioManager.Instance.IsAudioUnlocked;
                if (audible && !vehicle.Engine.isPlaying)
                    vehicle.Engine.Play();
                else if (!audible && vehicle.Engine.isPlaying)
                    vehicle.Engine.Stop();
            }
        }
    }

    AudioSource AddEngine(Transform vehicle)
    {
        var engine = vehicle.gameObject.AddComponent<AudioSource>();
        engine.clip = engineLoop;
        engine.playOnAwake = false;
        engine.loop = true;
        engine.spatialBlend = 1f;
        engine.dopplerLevel = 0f;
        engine.rolloffMode = AudioRolloffMode.Linear;
        engine.minDistance = 2f;
        engine.maxDistance = 25f;
        engine.volume = 0.11f * GameAudioManager.GetSfxVolume();
        engine.timeSamples = vehicle.GetSiblingIndex() * engineLoop.samples / (Fleet.Length * 2);
        // Start only after the first menu interaction and when Street is active.
        return engine;
    }

    static AudioClip CreateEngineLoop()
    {
        const int sampleRate = 24000;
        var samples = new float[sampleRate];
        for (int i = 0; i < samples.Length; i++)
        {
            float phase = i * (2f * Mathf.PI / sampleRate);
            float pulse = 0.72f + 0.18f * Mathf.Sin(11f * phase) + 0.10f * Mathf.Sin(23f * phase);
            samples[i] = pulse * (0.32f * Mathf.Sin(44f * phase) + 0.24f * Mathf.Sin(88f * phase) +
                0.15f * Mathf.Sin(132f * phase) + 0.10f * Mathf.Sin(176f * phase));
        }

        AudioClip clip = AudioClip.Create("Neighborhood engine", samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void OnDestroy()
    {
        if (engineLoop != null)
            Destroy(engineLoop);
    }

    sealed class Vehicle
    {
        public readonly Transform Transform;
        public readonly AudioSource Engine;
        public readonly float HalfLength;
        public readonly float CruiseSpeed;
        public float Distance;
        public float Speed;
        public float Front => Distance + HalfLength;
        public float Rear => Distance - HalfLength;

        public Vehicle(Transform transform, AudioSource engine, float distance, float halfLength, float cruiseSpeed)
        {
            Transform = transform;
            Engine = engine;
            Distance = distance;
            HalfLength = halfLength;
            CruiseSpeed = cruiseSpeed;
            Speed = cruiseSpeed;
        }
    }

    sealed class Lane
    {
        public readonly List<Vehicle> Vehicles = new List<Vehicle>(Fleet.Length);
        public readonly float Length;
        public readonly float CrossingEntrance;
        public readonly float CrossingExit;
        public float StopLine => CrossingEntrance - StopOffset;
        public bool FromLeft => direction > 0;
        public Vehicle PassingVehicle;

        readonly int direction;
        readonly float roadX;
        readonly float roadY;
        readonly float laneOffset;
        readonly float turnRadius;
        readonly float sideLength;
        readonly float straightStart;
        readonly float straightEnd;
        readonly float entryZ;
        readonly float exitZ;

        public Lane(Bounds road, int direction, float stripeStart, float stripeEnd)
        {
            this.direction = direction;
            roadX = road.center.x;
            roadY = road.max.y;
            laneOffset = road.extents.x * 0.5f;
            turnRadius = laneOffset * 1.6f;

            // Express both routes in their own forward direction. Each one enters and exits
            // via left turns at the outer junctions, keeping the moving traffic on its own side.
            entryZ = (direction > 0 ? road.min.z : -road.max.z) - laneOffset;
            exitZ = (direction > 0 ? road.max.z : -road.min.z) + laneOffset;
            sideLength = road.extents.x * 12.8f - laneOffset - turnRadius;
            float turnLength = Mathf.PI * 0.5f * turnRadius;
            float mainStreetStart = entryZ + turnRadius;
            straightStart = sideLength + turnLength;
            straightEnd = straightStart + (exitZ - turnRadius - mainStreetStart);
            Length = straightEnd + turnLength + sideLength;

            CrossingEntrance = straightStart + (direction > 0 ? stripeStart : -stripeEnd) - mainStreetStart;
            CrossingExit = straightStart + (direction > 0 ? stripeEnd : -stripeStart) - mainStreetStart;
        }

        public void Place(Transform vehicle, float distance)
        {
            float turnX = -laneOffset - turnRadius;
            float turnEnd = straightEnd + straightStart - sideLength;
            Vector2 position;
            Vector2 heading;
            if (distance < sideLength)
            {
                position = new Vector2(turnX - (sideLength - distance), entryZ);
                heading = Vector2.right;
            }
            else if (distance < straightStart)
            {
                float angle = (distance - sideLength) / turnRadius;
                heading = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                position = new Vector2(turnX + turnRadius * heading.y, entryZ + turnRadius * (1f - heading.x));
            }
            else if (distance < straightEnd)
            {
                position = new Vector2(-laneOffset, entryZ + turnRadius + (distance - straightStart));
                heading = Vector2.up;
            }
            else if (distance < turnEnd)
            {
                float angle = (distance - straightEnd) / turnRadius;
                heading = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle));
                position = new Vector2(turnX + turnRadius * heading.y, exitZ - turnRadius * (1f + heading.x));
            }
            else
            {
                position = new Vector2(turnX - (distance - turnEnd), exitZ);
                heading = Vector2.left;
            }

            vehicle.SetPositionAndRotation(
                new Vector3(roadX + position.x * direction, roadY, position.y * direction),
                Quaternion.LookRotation(new Vector3(heading.x * direction, 0f, heading.y * direction)));

            // The two ends of the side streets are separate. Mask the distant recycling point
            // gradually, while the playable length of the street always uses full-size traffic.
            float distanceFromEnd = Mathf.Min(distance, Length - distance);
            float visibility = Mathf.SmoothStep(0f, 1f, distanceFromEnd / RouteEndFadeDistance);
            vehicle.localScale = Vector3.one * visibility;
        }
    }
}
