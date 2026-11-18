using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the school street and its surrounding neighborhood around the playable crossing.
/// Menu: Tools > What Should I Do > Rebuild Street Layout
/// </summary>
public static class StreetLayoutBuilder
{
    const string ModelFolder = "Assets/Characters";
    const string MaterialFolder = "Assets/Materials";
    const string SettingsFolder = "Assets/Settings";
    const string LayoutRootName = "StreetLayout";
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    // Street metrics, read from the road objects already in SampleScene.
    const float GroundY = -0.1f;          // grass plane surface
    const float SidewalkTopY = 0.2f;      // sidewalks span |x| = 5..8, top at y = 0.2
    const float BuildingSetbackX = 12.3f; // facade line, leaves a grass verge for trees
    const float LampX = 8.45f;
    const float PathX = 6.3f;             // walking line on each sidewalk
    const float SouthJunctionZ = -59f;
    const float NorthJunctionZ = 56f;

    // Every model is authored facing +Z, so a left-side building turns to face +X and a
    // right-side one faces -X. If a building ends up with its back to the street, add 180.
    const float BuildingYawLeft = 90f;
    const float BuildingYawRight = -90f;

    static readonly Color GrassColor = new Color(0.39f, 0.67f, 0.38f);
    static readonly Color SidewalkColor = new Color(0.91f, 0.865f, 0.76f);
    static readonly Color RoadColor = new Color(0.34f, 0.385f, 0.45f);
    static readonly Color PaintColor = new Color(1f, 0.99f, 0.93f);

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
        if (Application.isBatchMode && scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath);

        if (scene.path != ScenePath)
        {
            Debug.LogError("[StreetLayout] Open SampleScene before rebuilding the street.");
            return;
        }

        var root = ResetLayoutRoot();
        var surfaceGroup = NewGroup(root, "Streetscape");
        var buildingGroup = NewGroup(root, "Buildings");
        var treeGroup = NewGroup(root, "Trees");
        var characterGroup = NewGroup(root, "Characters");
        var propGroup = NewGroup(root, "Props");
        var neighborhood = NewGroup(root, "Neighborhood");

        ExtendStreet(surfaceGroup);

        // The player walks the right sidewalk toward +Z: corner store on the same side,
        // school across the zebra crossing, houses filling the block behind.
        var buildings = new[]
        {
            new BuildingSpec("Store", 1, 23f, 6.4f),
            new BuildingSpec("HouseTwo", 1, 0f, 6.5f),
            new BuildingSpec("House", 1, -15f, 6.1f),
            new BuildingSpec("HouseTwo", 1, -35f, 6.2f),
            new BuildingSpec("House", 1, 36f, 6.7f),
            new BuildingSpec("HouseTwo", 1, 44f, 5.3f),
            new BuildingSpec("School", -1, 19f, 8.9f),
            new BuildingSpec("House", -1, 4f, 6.4f),
            new BuildingSpec("HouseTwo", -1, -13f, 6.3f),
            new BuildingSpec("House", -1, -32f, 5.7f),
            new BuildingSpec("HouseTwo", -1, 33f, 6.6f),
            new BuildingSpec("Store", -1, 43f, 6f),
        };

        foreach (var spec in buildings)
            PlaceBuilding(buildingGroup, spec);

        AddFrontagePaths(surfaceGroup);
        AddNeighborhood(neighborhood);

        var trees = new[]
        {
            new TreeSpec("TreeFive", 10.25f, 9.3f, 4.4f, 20f),
            new TreeSpec("TreeThree", 10.6f, 30f, 5.5f, 240f),
            new TreeSpec("TreeFive", 10.5f, -7.1f, 5.1f, 75f),
            new TreeSpec("TreeThree", 11f, -24f, 5.3f, 210f),
            new TreeSpec("TreeFive", -10.7f, 10.7f, 4.8f, 40f),
            new TreeSpec("TreeThree", -11f, 27.3f, 5.4f, 190f),
            new TreeSpec("TreeThree", -10.7f, -4.1f, 4.8f, 260f),
            new TreeSpec("TreeFive", -11f, -22f, 5.2f, 95f),
        };

        foreach (var spec in trees)
        {
            var tree = SpawnModel(spec.Model, treeGroup, new Vector3(spec.X, 0f, spec.Z), spec.Height, spec.Yaw, GroundY);
            if (tree != null)
                tree.name = $"{spec.Model} {treeGroup.childCount:00}";
        }

        var boyModel = SpawnModel("Boy", characterGroup, new Vector3(PathX, 0f, 6f), 1.5f, 0f, SidewalkTopY);
        var player = boyModel != null ? BuildPlayerRig(characterGroup, boyModel) : null;

        AddStreetLamps(propGroup);
        AddCrossingSigns(propGroup);
        AddStreetColliders(root);

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

    static void ExtendStreet(Transform parent)
    {
        var ground = GameObject.Find("Ground");
        ground.transform.localScale = new Vector3(32f, 1f, 28f);

        float start = SouthJunctionZ + 5f;
        float end = NorthJunctionZ - 5f;
        float length = end - start;
        float center = (start + end) * 0.5f;
        ResizeStreetObject("MainRoad", center, length);
        ResizeStreetObject("LeftSidewalk", center, length);
        ResizeStreetObject("RightSidewalk", center, length);

        var asphalt = GetStreetMaterial("RoadMaterial");
        var sidewalk = GetStreetMaterial("New Material");
        var paint = GetStreetMaterial("WhitePaint");
        foreach (float junction in new[] { SouthJunctionZ, NorthJunctionZ })
        {
            var crossStreet = NewGroup(parent, junction > 0f ? "North side street" : "South side street");
            CreatePrimitive(crossStreet, PrimitiveType.Cube, "Road",
                new Vector3(0f, 0f, junction), new Vector3(136f, 0.2f, 10f), asphalt);

            float outside = Mathf.Sign(junction);
            CreatePrimitive(crossStreet, PrimitiveType.Cube, "Outer sidewalk",
                new Vector3(0f, 0.05f, junction + outside * 6.5f), new Vector3(136f, 0.3f, 3f), sidewalk);

            foreach (int side in new[] { -1, 1 })
            {
                CreatePrimitive(crossStreet, PrimitiveType.Cube, "Block corner",
                    new Vector3(side * 38f, 0.05f, junction - outside * 6.5f), new Vector3(60f, 0.3f, 3f), sidewalk);

                for (int x = 14; x < 68; x += 6)
                {
                    var dash = CreatePrimitive(crossStreet, PrimitiveType.Cube, "Lane divider",
                        new Vector3(side * x, 0.105f, junction), new Vector3(2.5f, 0.01f, 0.19f), paint);
                    dash.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        for (int z = -50; z <= 48; z += 5)
        {
            if (z >= -25 && z <= 25)
                continue;

            CreatePrimitive(parent, PrimitiveType.Cube, "Lane divider", new Vector3(0f, 0.105f, z),
                new Vector3(0.3f, 0.01f, 1.5f), paint).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    static void ResizeStreetObject(string name, float center, float length)
    {
        var surface = GameObject.Find(name).transform;
        Vector3 position = surface.position;
        Vector3 scale = surface.localScale;
        position.z = center;
        scale.z = length;
        surface.position = position;
        surface.localScale = scale;
    }

    static Material GetStreetMaterial(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
    }

    static void AddFrontagePaths(Transform parent)
    {
        var paving = GetOrCreateMaterial("FrontagePaving", new Color(0.76f, 0.76f, 0.66f), 0.13f);
        var seams = GetOrCreateMaterial("PavementJoints", new Color(0.76f, 0.705f, 0.61f), 0.05f);
        var gardens = NewGroup(parent, "Building entrances");
        CreatePrimitive(gardens, PrimitiveType.Cube, "School entrance", new Vector3(-10.2f, GroundY, 18.7f),
            new Vector3(4.55f, 0.17f, 5.4f), paving);
        CreatePrimitive(gardens, PrimitiveType.Cube, "Store entrance", new Vector3(10.2f, GroundY, 23.5f),
            new Vector3(4.55f, 0.17f, 5.1f), paving);

        foreach (int side in new[] { -1, 1 })
        {
            for (int z = -39; z <= 48; z += 3)
            {
                var joint = CreatePrimitive(parent, PrimitiveType.Cube, "Sidewalk joint",
                    new Vector3(side * 6.5f, SidewalkTopY, z + 0.2f), new Vector3(2.94f, 0.007f, 0.032f), seams);
                joint.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }

    static void AddNeighborhood(Transform parent)
    {
        var homes = NewGroup(parent, "Houses behind the main street");
        foreach (int side in new[] { -1, 1 })
        {
            SpawnModel("HouseTwo", homes, new Vector3(side * 24f, 0f, 1f), 6.8f, side * -55f, GroundY);
            SpawnModel("House", homes, new Vector3(side * 26f, 0f, 20f), 6.6f, side * -80f, GroundY);
            SpawnModel("HouseTwo", homes, new Vector3(side * 27f, 0f, 36f), 6.85f, side * -110f, GroundY);
            SpawnModel("Store", homes, new Vector3(side * 26f, 0f, 68f), 6.5f, 180f, GroundY);
            SpawnModel("House", homes, new Vector3(side * 14f, 0f, 69f), 6.6f, 180f + side * 15f, GroundY);
            SpawnModel("HouseTwo", homes, new Vector3(side * 43f, 0f, 69f), 7f, 180f, GroundY);
        }

        var trees = NewGroup(parent, "Neighborhood treeline");
        var trunk = GetOrCreateMaterial("NeighborhoodBark", new Color(0.38f, 0.26f, 0.19f), 0.04f);
        var leaf = GetOrCreateMaterial("NeighborhoodLeaf", new Color(0.28f, 0.59f, 0.36f), 0.08f);
        var lightLeaf = GetOrCreateMaterial("NeighborhoodLeafLight", new Color(0.43f, 0.68f, 0.39f), 0.08f);
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < 20; i++)
            {
                float x = side * (31f + i % 3 * 9f);
                float z = -44f + i * 6.8f;
                float height = 5.9f + i % 5 * 0.75f;
                AddNeighborhoodTree(trees, trunk, i % 2 == 0 ? leaf : lightLeaf, x, z, height);
            }
        }

        // A grove behind the side street gives the crossing a developed view to the horizon.
        for (int i = 0; i < 19; i++)
        {
            float x = (i - 9) * 6.3f;
            AddNeighborhoodTree(trees, trunk, i % 2 == 0 ? lightLeaf : leaf, x, 82f + i % 3 * 4.75f,
                8f + i % 5 * 0.8f);
        }

        var hills = NewGroup(parent, "Park ridge");
        foreach (int side in new[] { -1, 1 })
        {
            CreatePrimitive(hills, PrimitiveType.Sphere, "Outer hill", new Vector3(side * 69f, -7f, 100f),
                new Vector3(108f, 27f, 60f), leaf).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        CreatePrimitive(hills, PrimitiveType.Sphere, "Center hill", new Vector3(0f, -7f, 113f),
            new Vector3(125f, 30f, 70f), lightLeaf).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void AddNeighborhoodTree(Transform parent, Material bark, Material leaf, float x, float z, float height)
    {
        var tree = NewGroup(parent, "Tree");
        var trunk = CreatePrimitive(tree, PrimitiveType.Cylinder, "Trunk",
            new Vector3(x, GroundY + height * 0.25f, z), new Vector3(height * 0.075f, height * 0.25f, height * 0.075f), bark);
        var crown = CreatePrimitive(tree, PrimitiveType.Sphere, "Crown",
            new Vector3(x, GroundY + height * 0.69f, z), new Vector3(height * 0.67f, height * 0.62f, height * 0.61f), leaf);
        trunk.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        crown.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
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

    static Bounds MeasureBounds(GameObject instance, Transform relativeTo = null)
    {
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;

        foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null)
                continue;

            var local = filter.sharedMesh.bounds;
            var matrix = filter.transform.localToWorldMatrix;
            if (relativeTo != null)
                matrix = relativeTo.worldToLocalMatrix * matrix;

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

    [MenuItem("Tools/What Should I Do/Refresh Street Colliders", priority = 15)]
    public static void RefreshStreetColliders()
    {
        var scene = Application.isBatchMode
            ? EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity")
            : SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != LayoutRootName)
                continue;

            AddStreetColliders(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        throw new System.InvalidOperationException("The open scene is missing StreetLayout.");
    }

    static void AddStreetColliders(Transform layout)
    {
        var existing = layout.Find("Street Colliders");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        var colliders = NewGroup(layout, "Street Colliders");
        foreach (Transform building in layout.Find("Buildings"))
            AddBoxCollider(colliders, building, new Vector3(0.9f, 0.85f, 0.9f));

        foreach (Transform tree in layout.Find("Trees"))
            AddBoxCollider(colliders, tree, new Vector3(0.68f, 0.27f, 0.32f));

        foreach (Transform prop in layout.Find("Props"))
        {
            var post = prop.Find("Post");
            if (post != null)
                AddBoxCollider(colliders, post, Vector3.one);
        }
    }

    static void AddBoxCollider(Transform group, Transform model, Vector3 fit)
    {
        Bounds bounds = MeasureBounds(model.gameObject, model);
        if (bounds.size == Vector3.zero)
            return;

        Vector3 size = Vector3.Scale(bounds.size, fit);
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y + size.y * 0.5f, bounds.center.z);
        var collision = NewGroup(group, model.parent.name + " / " + model.name);
        collision.SetPositionAndRotation(model.TransformPoint(center), model.rotation);
        collision.gameObject.AddComponent<BoxCollider>().size = Vector3.Scale(size, model.lossyScale);
    }

    static void AddCrossingSigns(Transform parent)
    {
        var metal = GetOrCreateMaterial("LampPost", new Color(0.18f, 0.27f, 0.29f), 0.25f);
        var yellow = GetOrCreateMaterial("CrossingSignYellow", new Color(1f, 0.73f, 0.23f), 0.16f);

        foreach (var position in new[] { new Vector3(8.75f, 0f, 15.7f), new Vector3(-8.75f, 0f, 19.8f) })
        {
            var sign = NewGroup(parent, "Crossing sign");
            CreatePrimitive(sign, PrimitiveType.Cylinder, "Signpost", position + Vector3.up * 1.35f,
                new Vector3(0.08f, 1.45f, 0.08f), metal);

            Vector3 center = position + Vector3.up * 2.65f;
            CreatePrimitive(sign, PrimitiveType.Cube, "Diamond border", center, new Vector3(0.78f, 0.78f, 0.09f), metal)
                .transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            CreatePrimitive(sign, PrimitiveType.Cube, "Reflective face", center,
                new Vector3(0.71f, 0.71f, 0.112f), yellow).transform.rotation = Quaternion.Euler(0f, 0f, 45f);

            foreach (int face in new[] { -1, 1 })
            {
                Vector3 icon = center + Vector3.forward * (face * 0.065f);
                CreatePrimitive(sign, PrimitiveType.Sphere, "Walker head", icon + Vector3.up * 0.23f,
                    new Vector3(0.13f, 0.13f, 0.03f), metal);
                DrawSignStroke(sign, metal, icon, new Vector2(-0.035f, 0.12f), new Vector2(0.03f, -0.075f));
                DrawSignStroke(sign, metal, icon, new Vector2(-0.035f, 0.11f), new Vector2(-0.155f, -0.03f));
                DrawSignStroke(sign, metal, icon, new Vector2(-0.035f, 0.10f), new Vector2(0.16f, 0.04f));
                DrawSignStroke(sign, metal, icon, new Vector2(0.03f, -0.07f), new Vector2(-0.08f, -0.26f));
                DrawSignStroke(sign, metal, icon, new Vector2(0.03f, -0.07f), new Vector2(0.18f, -0.23f));
            }
        }
    }

    static void DrawSignStroke(Transform sign, Material material, Vector3 center, Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        var stroke = CreatePrimitive(sign, PrimitiveType.Cube, "Walker symbol", center + (Vector3)((start + end) * 0.5f),
            new Vector3(0.046f, direction.magnitude, 0.023f), material);
        stroke.transform.rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
        stroke.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    static void AddStreetLamps(Transform parent)
    {
        var postMaterial = GetOrCreateMaterial("LampPost", new Color(0.18f, 0.27f, 0.29f), 0.25f);
        var glowMaterial = GetOrCreateMaterial("LampGlow", new Color(1f, 0.93f, 0.77f), 0.28f,
            new Color(0.52f, 0.42f, 0.25f));
        if (postMaterial == null)
            return;

        var lampZ = new[] { -37f, -17f, 4.5f, 28.5f, 48f };
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < lampZ.Length; i++)
            {
                float x = side * LampX;
                var lamp = NewGroup(parent, $"Street Lamp {(side < 0 ? "Left" : "Right")} {i + 1}");

                CreatePrimitive(lamp, PrimitiveType.Cylinder, "Post",
                    new Vector3(x, GroundY + 1.96f, lampZ[i]), new Vector3(0.12f, 1.99f, 0.12f), postMaterial);
                CreatePrimitive(lamp, PrimitiveType.Cylinder, "Post base",
                    new Vector3(x, GroundY + 0.24f, lampZ[i]), new Vector3(0.28f, 0.25f, 0.28f), postMaterial);
                CreatePrimitive(lamp, PrimitiveType.Cylinder, "Lamp cap",
                    new Vector3(x, GroundY + 4.32f, lampZ[i]), new Vector3(0.56f, 0.04f, 0.56f), postMaterial);

                if (glowMaterial == null)
                    continue;

                var globe = CreatePrimitive(lamp, PrimitiveType.Cylinder, "Lamp glass",
                    new Vector3(x, GroundY + 4.09f, lampZ[i]), new Vector3(0.36f, 0.18f, 0.36f), glowMaterial);
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
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.77f, 0.89f, 0.89f);
        RenderSettings.fogStartDistance = 55f;
        RenderSettings.fogEndDistance = 150f;

        var skyMaterial = GetOrCreateSkyMaterial();
        if (skyMaterial != null)
        {
            RenderSettings.skybox = skyMaterial;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.69f, 0.81f, 0.89f);
            RenderSettings.ambientEquatorColor = new Color(0.54f, 0.58f, 0.59f);
            RenderSettings.ambientGroundColor = new Color(0.34f, 0.36f, 0.29f);
        }

        var sunObject = GameObject.Find("Directional Light");
        var sun = sunObject != null ? sunObject.GetComponent<Light>() : null;
        if (sun == null)
        {
            Debug.LogWarning("[StreetLayout] Directional Light not found, sun left as is.");
            return;
        }

        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.97f, 0.89f);
        sun.intensity = 1.38f;
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
        var shader = Shader.Find("What Should I Do/Neighborhood Sky");
        if (shader == null)
        {
            Debug.LogWarning("[StreetLayout] Neighborhood Sky shader not found, keeping the current sky.");
            return material;
        }

        if (material == null)
        {
            material = new Material(shader) { name = "Sky_BrightCartoon" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetColor("_HorizonColor", RenderSettings.fogColor);
        material.SetColor("_ZenithColor", new Color(0.36f, 0.69f, 0.89f));
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

    /// Both avatars share a controller and a foot-level pivot.
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

        var girl = SpawnModel("Girl", playerObject.transform, playerObject.transform.position, 1.5f, 0f, SidewalkTopY);
        if (girl != null)
        {
            girl.name = "Girl Model";
            girl.SetActive(false);
            walker.SetGirlModel(girl.transform);
        }

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
            camera.fieldOfView = 63f;
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

        follow.offset = new Vector3(-2.2f, 6.4f, -9.5f);
        follow.lookHeight = 1f;
        follow.lookSide = -2.75f;
        follow.lookAhead = 6.5f;
        follow.damping = 7f;

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
