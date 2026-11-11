using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds SampleScene as a bright cartoon town block: buildings, trees and characters are
/// placed around the existing road, the street materials and sky get a kid-friendly palette,
/// and the main camera is wired to follow the player.
/// Menu: Tools > What Should I Do > Rebuild Street Layout
/// </summary>
public static class StreetLayoutBuilder
{
    const string ModelFolder = "Assets/Characters";
    const string MaterialFolder = "Assets/Materials";
    const string SettingsFolder = "Assets/Settings";
    const string LayoutRootName = "StreetLayout";

    // Street metrics, read from the road objects already in SampleScene.
    const float GroundY = -0.1f;          // grass plane surface
    const float SidewalkTopY = 0.2f;      // sidewalks span |x| = 5..8, top at y = 0.2
    const float BuildingSetbackX = 11.5f; // facade line, leaves a grass verge for trees
    const float VergeTreeX = 9.9f;
    const float LampX = 7.5f;
    const float PathX = 6.3f;             // walking line on each sidewalk

    // Every model is authored facing +Z, so a left-side building turns to face +X and a
    // right-side one faces -X. If a building ends up with its back to the street, add 180.
    const float BuildingYawLeft = 90f;
    const float BuildingYawRight = -90f;

    static readonly Color GrassColor = new Color(0.45f, 0.82f, 0.42f);
    static readonly Color SidewalkColor = new Color(0.94f, 0.92f, 0.84f);
    static readonly Color RoadColor = new Color(0.30f, 0.33f, 0.42f);
    static readonly Color PaintColor = new Color(0.99f, 0.99f, 0.96f);

    struct BuildingSpec
    {
        public readonly string Model;
        public readonly int Side;
        public readonly float Z;
        public readonly float Height;

        public BuildingSpec(string model, int side, float z, float height)
        {
            Model = model;
            Side = side;
            Z = z;
            Height = height;
        }
    }

    struct TreeSpec
    {
        public readonly string Model;
        public readonly float X;
        public readonly float Z;
        public readonly float Height;
        public readonly float Yaw;

        public TreeSpec(string model, float x, float z, float height, float yaw)
        {
            Model = model;
            X = x;
            Z = z;
            Height = height;
            Yaw = yaw;
        }
    }

    [MenuItem("Tools/What Should I Do/Rebuild Street Layout", priority = 10)]
    public static void RebuildStreetLayout()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("[StreetLayout] No scene is open.");
            return;
        }

        var root = ResetLayoutRoot();
        var buildingGroup = NewGroup(root, "Buildings");
        var treeGroup = NewGroup(root, "Trees");
        var characterGroup = NewGroup(root, "Characters");
        var propGroup = NewGroup(root, "Props");

        // The player walks the right sidewalk toward +Z: corner store on the same side,
        // school across the zebra crossing, houses filling the block behind.
        var buildings = new[]
        {
            new BuildingSpec("Store", 1, 21f, 6.6f),
            new BuildingSpec("HouseTwo", 1, -5f, 6f),
            new BuildingSpec("House", 1, -17f, 5.4f),
            new BuildingSpec("School", -1, 22f, 9f),
            new BuildingSpec("House", -1, 3f, 5.6f),
            new BuildingSpec("HouseTwo", -1, -16f, 6.2f),
        };

        foreach (var spec in buildings)
            PlaceBuilding(buildingGroup, spec);

        // Verge trees only sit in the gaps between building footprints; the outer rows
        // give the block a treeline instead of an empty green horizon.
        var trees = new[]
        {
            new TreeSpec("TreeFive", VergeTreeX, 7.5f, 5f, 20f),
            new TreeSpec("TreeThree", VergeTreeX, 10.5f, 5.4f, 140f),
            new TreeSpec("TreeFive", VergeTreeX, 13.5f, 4.8f, 75f),
            new TreeSpec("TreeThree", VergeTreeX, -11.5f, 5.2f, 210f),
            new TreeSpec("TreeThree", VergeTreeX, 28f, 5.8f, 300f),
            new TreeSpec("TreeThree", -VergeTreeX, 9.5f, 5.2f, 320f),
            new TreeSpec("TreeFive", -VergeTreeX, 12.5f, 4.8f, 55f),
            new TreeSpec("TreeFive", -VergeTreeX, -4f, 5.4f, 190f),
            new TreeSpec("TreeThree", -VergeTreeX, -8f, 5f, 260f),
            new TreeSpec("TreeThree", -VergeTreeX, -24f, 5.6f, 95f),
            new TreeSpec("TreeFive", -VergeTreeX, -27f, 5.2f, 15f),
            new TreeSpec("TreeThree", 20.5f, 31f, 8.2f, 40f),
            new TreeSpec("TreeFive", 21.5f, 10f, 7.8f, 165f),
            new TreeSpec("TreeThree", 20f, -8f, 8.4f, 280f),
            new TreeSpec("TreeFive", 21f, -28f, 8f, 90f),
            new TreeSpec("TreeFive", -20.5f, 32f, 8.5f, 15f),
            new TreeSpec("TreeThree", -21.5f, 11f, 7.6f, 235f),
            new TreeSpec("TreeFive", -20f, -6f, 8f, 310f),
            new TreeSpec("TreeThree", -21f, -27f, 8.6f, 120f),
        };

        foreach (var spec in trees)
        {
            var tree = SpawnModel(spec.Model, treeGroup, new Vector3(spec.X, 0f, spec.Z), spec.Height, spec.Yaw, GroundY);
            if (tree != null)
                tree.name = $"{spec.Model} {treeGroup.childCount:00}";
        }

        var boyModel = SpawnModel("Boy", characterGroup, new Vector3(PathX, 0f, 6f), 1.5f, 0f, SidewalkTopY);
        var player = boyModel != null ? BuildPlayerRig(characterGroup, boyModel) : null;

        var friend = SpawnModel("Girl", characterGroup, new Vector3(-PathX, 0f, 11.5f), 1.42f, 180f, SidewalkTopY);
        if (friend != null)
            friend.name = "Girl";

        AddClouds(propGroup);
        AddBushes(propGroup);
        AddStreetLamps(propGroup);

        CleanStrayDuplicates();

        ApplyLook();
        SetupCamera(player);
        CreateCrossingQuiz(root);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[StreetLayout] Rebuilt: {buildingGroup.childCount} buildings, {treeGroup.childCount} trees, " +
                  $"{characterGroup.childCount} characters, {propGroup.childCount} prop groups.");
    }

    [MenuItem("Tools/What Should I Do/Apply Bright Cartoon Look", priority = 11)]
    public static void ApplyLookOnly()
    {
        ApplyLook();

        var scene = SceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        Debug.Log("[StreetLayout] Bright cartoon look applied.");
    }

    static void ApplyLook()
    {
        TintStreetMaterial("GreenPaint", GrassColor, 0.05f);
        TintStreetMaterial("New Material", SidewalkColor, 0.12f);
        TintStreetMaterial("RoadMaterial", RoadColor, 0.22f);
        TintStreetMaterial("WhitePaint", PaintColor, 0.2f);
        ApplySkyAndSun();
        AssetDatabase.SaveAssets();
    }

    static Transform ResetLayoutRoot()
    {
        var existing = GameObject.Find(LayoutRootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        return new GameObject(LayoutRootName).transform;
    }

    static Transform NewGroup(Transform parent, string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    static GameObject PlaceBuilding(Transform parent, BuildingSpec spec)
    {
        float yaw = spec.Side < 0 ? BuildingYawLeft : BuildingYawRight;
        var building = SpawnModel(spec.Model, parent, new Vector3(spec.Side * BuildingSetbackX, 0f, spec.Z),
            spec.Height, yaw, GroundY);
        if (building == null)
            return null;

        // Slide along X until the facade that faces the street lands on the setback line,
        // which keeps buildings clear of the sidewalk no matter how wide the model is.
        var bounds = MeasureBounds(building);
        float shift = spec.Side < 0
            ? -BuildingSetbackX - bounds.max.x
            : BuildingSetbackX - bounds.min.x;
        building.transform.position += Vector3.right * shift;

        return building;
    }

    static GameObject SpawnModel(string modelName, Transform parent, Vector3 position, float targetHeight, float yaw, float groundY)
    {
        var source = LoadModel(modelName);
        if (source == null)
        {
            Debug.LogError($"[StreetLayout] Could not load {ModelFolder}/{modelName}.glb");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        instance.name = modelName;
        instance.transform.localScale = Vector3.one;
        instance.transform.SetPositionAndRotation(new Vector3(position.x, 0f, position.z), Quaternion.Euler(0f, yaw, 0f));

        // The source models are all authored around 0.1-0.5 units tall, so fit each one
        // to the height that reads correctly in the street instead of trusting their scale.
        var bounds = MeasureBounds(instance);
        if (bounds.size.y > 0.0001f)
            instance.transform.localScale = Vector3.one * (targetHeight / bounds.size.y);

        bounds = MeasureBounds(instance);
        instance.transform.position += Vector3.up * (groundY - bounds.min.y - 0.01f);

        return instance;
    }

    static GameObject LoadModel(string modelName)
    {
        string path = $"{ModelFolder}/{modelName}.glb";
        var main = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (main != null)
            return main;

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is GameObject candidate && candidate.transform.parent == null)
                return candidate;

        return null;
    }

    static Bounds MeasureBounds(GameObject instance)
    {
        var bounds = new Bounds(instance.transform.position, Vector3.zero);
        bool hasBounds = false;

        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            var local = filter.sharedMesh.bounds;
            var matrix = filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z);
                point = matrix.MultiplyPoint3x4(point);

                if (hasBounds)
                    bounds.Encapsulate(point);
                else
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }

        return bounds;
    }

    static void AddClouds(Transform parent)
    {
        var cloudMaterial = GetOrCreateMaterial("Cloud", new Color(0.98f, 0.99f, 1f), 0f);
        if (cloudMaterial == null)
            return;

        var centers = new[]
        {
            new Vector3(-18f, 26f, 34f), new Vector3(4f, 30f, 42f), new Vector3(22f, 24f, 22f),
            new Vector3(-8f, 31f, -18f), new Vector3(17f, 27f, -34f),
        };
        var puffOffsets = new[] { Vector3.zero, new Vector3(5.5f, 0.6f, 0.4f), new Vector3(-5.2f, -0.4f, -0.6f) };
        var puffSizes = new[] { new Vector3(9f, 4.4f, 6f), new Vector3(7f, 3.4f, 5f), new Vector3(6.4f, 3f, 4.6f) };

        for (int i = 0; i < centers.Length; i++)
        {
            var cluster = NewGroup(parent, $"Cloud {i + 1}");
            for (int p = 0; p < puffOffsets.Length; p++)
            {
                var puff = CreatePrimitive(cluster, PrimitiveType.Sphere, $"Puff {p + 1}",
                    centers[i] + puffOffsets[p], puffSizes[p], cloudMaterial);

                var renderer = puff.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }

    static void AddBushes(Transform parent)
    {
        var bushMaterial = GetOrCreateMaterial("Bush", new Color(0.30f, 0.70f, 0.35f), 0.05f);
        var flowerMaterial = GetOrCreateMaterial("Flower", new Color(1f, 0.55f, 0.72f), 0.1f,
            new Color(0.7f, 0.18f, 0.34f));
        if (bushMaterial == null)
            return;

        var spots = new[]
        {
            new Vector3(8.7f, 0f, 4.5f), new Vector3(8.7f, 0f, 15.5f), new Vector3(8.7f, 0f, -11.5f),
            new Vector3(8.7f, 0f, -22f), new Vector3(-8.7f, 0f, 4.5f), new Vector3(-8.7f, 0f, 15.5f),
            new Vector3(-8.7f, 0f, -11.5f), new Vector3(-8.7f, 0f, -22f),
        };
        var flowerOffsets = new[]
        {
            new Vector3(0.32f, 0.32f, 0.1f), new Vector3(-0.26f, 0.3f, -0.22f), new Vector3(0.02f, 0.34f, -0.3f),
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var bush = NewGroup(parent, $"Bush {i + 1}");
            CreatePrimitive(bush, PrimitiveType.Sphere, "Leaves",
                new Vector3(spots[i].x, GroundY + 0.45f, spots[i].z), new Vector3(1.7f, 1.1f, 1.7f), bushMaterial);

            if (flowerMaterial == null)
                continue;

            foreach (var offset in flowerOffsets)
                CreatePrimitive(bush, PrimitiveType.Sphere, "Flower",
                    new Vector3(spots[i].x + offset.x, GroundY + 0.75f + offset.y, spots[i].z + offset.z),
                    Vector3.one * 0.24f, flowerMaterial);
        }
    }

    static void AddStreetLamps(Transform parent)
    {
        var postMaterial = GetOrCreateMaterial("LampPost", new Color(0.22f, 0.26f, 0.33f), 0.4f);
        var glowMaterial = GetOrCreateMaterial("LampGlow", new Color(1f, 0.88f, 0.6f), 0.5f,
            new Color(3.2f, 2.4f, 1.1f));
        if (postMaterial == null)
            return;

        var lampZ = new[] { -13f, 8f, 27f };
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < lampZ.Length; i++)
            {
                float x = side * LampX;
                var lamp = NewGroup(parent, $"Street Lamp {(side < 0 ? "Left" : "Right")} {i + 1}");

                CreatePrimitive(lamp, PrimitiveType.Cylinder, "Post",
                    new Vector3(x, SidewalkTopY + 2.1f, lampZ[i]), new Vector3(0.16f, 2.1f, 0.16f), postMaterial);

                if (glowMaterial == null)
                    continue;

                var globe = CreatePrimitive(lamp, PrimitiveType.Sphere, "Globe",
                    new Vector3(x, SidewalkTopY + 4.35f, lampZ[i]), Vector3.one * 0.42f, glowMaterial);
                globe.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    static GameObject CreatePrimitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
    {
        var instance = GameObject.CreatePrimitive(type);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.localScale = scale;

        var collider = instance.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        var renderer = instance.GetComponent<Renderer>();
        if (renderer != null && material != null)
            renderer.sharedMaterial = material;

        return instance;
    }

    static Material GetOrCreateMaterial(string name, Color color, float smoothness, Color? emission = null)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[StreetLayout] URP Lit shader not found, props keep their default material.");
                return null;
            }

            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    static void TintStreetMaterial(string materialName, Color color, float smoothness)
    {
        string path = $"{MaterialFolder}/{materialName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Debug.LogWarning($"[StreetLayout] Missing street material {path}");
            return;
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
    }

    static void ApplySkyAndSun()
    {
        var skyMaterial = GetOrCreateSkyMaterial();
        if (skyMaterial != null)
        {
            RenderSettings.skybox = skyMaterial;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
        }

        var sunObject = GameObject.Find("Directional Light");
        var sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
        if (sun == null)
        {
            Debug.LogWarning("[StreetLayout] Directional Light not found, sun left as is.");
            return;
        }

        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = 1.7f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.7f;
        sunObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        RenderSettings.sun = sun;
        DynamicGI.UpdateEnvironment();
    }

    static Material GetOrCreateSkyMaterial()
    {
        string path = $"{SettingsFolder}/Sky_BrightCartoon.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[StreetLayout] Skybox/Procedural shader not found, keeping the current sky.");
                return null;
            }

            material = new Material(shader) { name = "Sky_BrightCartoon" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_SkyTint", new Color(0.36f, 0.63f, 0.95f));
        material.SetColor("_GroundColor", new Color(0.66f, 0.78f, 0.90f));
        material.SetFloat("_AtmosphereThickness", 1.15f);
        material.SetFloat("_Exposure", 1.35f);
        material.SetFloat("_SunSize", 0.045f);
        EditorUtility.SetDirty(material);

        return material;
    }

    [MenuItem("Tools/What Should I Do/Add Crossing Quiz", priority = 14)]
    public static void AddCrossingQuizToScene()
    {
        var root = GameObject.Find(LayoutRootName);
        if (root == null)
        {
            Debug.LogError("[StreetLayout] No StreetLayout in this scene. Run Rebuild Street Layout first.");
            return;
        }

        if (!CreateCrossingQuiz(root.transform))
        {
            Debug.Log("[StreetLayout] Crossing quiz is already in the scene.");
            return;
        }

        SaveOpenScene();
        Debug.Log("[StreetLayout] Crossing quiz ready: walk to the zebra crossing and answer with the mouse or 1/2/3.");
    }

    static bool CreateCrossingQuiz(Transform root)
    {
        if (root.Find("Crossing Quiz") != null)
            return false;

        var quizObject = new GameObject("Crossing Quiz");
        quizObject.transform.SetParent(root, false);
        quizObject.AddComponent<CrossingQuiz>();
        return true;
    }

    [MenuItem("Tools/What Should I Do/Setup Player Rig", priority = 12)]
    public static void SetupPlayerRigInOpenScene()
    {
        var characters = FindGroup("Characters");
        if (characters == null)
        {
            Debug.LogError("[StreetLayout] This scene has no StreetLayout/Characters. Run Rebuild Street Layout first.");
            return;
        }

        var player = characters.Find("Player");
        if (player == null)
        {
            var model = characters.Find("Boy Model");
            if (model == null)
                model = characters.Find("Player (Boy)");

            if (model == null)
            {
                Debug.LogError("[StreetLayout] Could not find the boy model under StreetLayout/Characters.");
                return;
            }

            player = BuildPlayerRig(characters, model.gameObject);
        }

        SetupCamera(player);
        SaveOpenScene();
        Debug.Log("[StreetLayout] Player rig ready: CharacterController + PlayerWalker, camera following the Player root.");
    }

    [MenuItem("Tools/What Should I Do/Clean Stray Duplicates", priority = 13)]
    public static void CleanStrayDuplicates()
    {
        var modelNames = new[] { "School", "Store", "House", "HouseTwo", "TreeThree", "TreeFive", "Boy", "Girl" };
        var scene = SceneManager.GetActiveScene();
        int removed = 0;

        foreach (var rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.transform.childCount == 0 && !rootObject.GetComponent<Renderer>())
                continue;

            foreach (var modelName in modelNames)
            {
                if (rootObject.name != modelName && !rootObject.name.StartsWith(modelName + " ("))
                    continue;

                Object.DestroyImmediate(rootObject);
                removed++;
                break;
            }
        }

        if (removed == 0)
        {
            Debug.Log("[StreetLayout] No stray model duplicates at the scene root.");
            return;
        }

        SaveOpenScene();
        Debug.Log($"[StreetLayout] Removed {removed} stray model duplicates from the scene root.");
    }

    /// Wraps the model in a pivot at his feet, so movement and the camera use a sensible origin
    /// instead of the model's mid-body pivot.
    static Transform BuildPlayerRig(Transform characters, GameObject model)
    {
        var playerObject = new GameObject("Player");
        playerObject.transform.SetParent(characters, false);
        playerObject.transform.position = new Vector3(model.transform.position.x, SidewalkTopY, model.transform.position.z);

        model.name = "Boy Model";
        model.transform.SetParent(playerObject.transform, true);

        var body = playerObject.AddComponent<CharacterController>();
        body.height = 1.5f;
        body.radius = 0.28f;
        body.center = new Vector3(0f, 0.75f, 0f);
        body.stepOffset = 0.35f;
        body.skinWidth = 0.03f;

        var walker = playerObject.AddComponent<PlayerWalker>();
        walker.walkSpeed = 3.2f;
        walker.model = model.transform;

        return playerObject.transform;
    }

    static Transform FindGroup(string groupName)
    {
        var root = GameObject.Find(LayoutRootName);
        return root != null ? root.transform.Find(groupName) : null;
    }

    static void SaveOpenScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
            return;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetupCamera(Transform player)
    {
        var cameraObject = GameObject.Find("Main Camera");
        if (cameraObject == null)
        {
            Debug.LogWarning("[StreetLayout] Main Camera not found.");
            return;
        }

        var camera = cameraObject.GetComponent<Camera>();
        if (camera != null)
        {
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 400f;
        }

        var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }

        var follow = cameraObject.GetComponent<ThirdPersonFollowCamera>();
        if (follow == null)
            follow = cameraObject.AddComponent<ThirdPersonFollowCamera>();

        follow.offset = new Vector3(0.6f, 3.6f, -7.5f);
        follow.lookHeight = 1.4f;
        follow.lookAhead = 5f;

        if (player != null)
        {
            follow.target = player;
            follow.SnapToTarget();
        }
        else
        {
            Debug.LogWarning("[StreetLayout] Player not found, camera left where it was.");
        }
    }
}
