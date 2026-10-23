using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MidtermSevenLevelBuilder
{
    private const string SourceScene = "Assets/Scenes/MarioClone.unity";
    private const string LevelFolder = "Assets/Scenes/Levels";
    private const string RawFolder = "Assets/MidtermSprites/Raw";
    private const string EnemyFolder = "Assets/Prefabs/Enemies";
    private const float FloorY = -3.2f;
    private const float PlayerStartX = -7f;
    private const float LevelEndX = 108f;

    private static readonly string[] LevelNames =
    {
        "GreenFields",
        "ForestCaves",
        "WaterfallCliffs",
        "SnowMountain",
        "DesertRuins",
        "FireCastle",
        "SkyIslands"
    };

    private struct Segment
    {
        public float Start;
        public float End;
        public float Top;

        public Segment(float start, float end, float top)
        {
            Start = start;
            End = end;
            Top = top;
        }
    }

    private struct PlatformSpec
    {
        public float X;
        public float Y;
        public bool Moving;

        public PlatformSpec(float x, float y, bool moving = true)
        {
            X = x;
            Y = y;
            Moving = moving;
        }
    }

    private struct EnemySpec
    {
        public string Kind;
        public string SpriteName;
        public float X;
        public float Y;
        public bool Flying;

        public EnemySpec(
            string kind,
            string spriteName,
            float x,
            float y,
            bool flying = false
        )
        {
            Kind = kind;
            SpriteName = spriteName;
            X = x;
            Y = y;
            Flying = flying;
        }
    }

    private sealed class LevelPlan
    {
        public int Number;
        public string DisplayName;
        public string BackgroundPath;
        public string GroundSprite;
        public string HazardSprite;
        public string PlatformAtlas = "Grounds";
        public string PlatformSprite = "Grounds_053";
        public float PlatformScale = 1f;
        public Color CameraColor;
        public readonly List<Segment> Segments = new List<Segment>();
        public readonly List<PlatformSpec> Platforms =
            new List<PlatformSpec>();
        public readonly List<EnemySpec> Enemies = new List<EnemySpec>();
        public readonly List<float> SpikeXs = new List<float>();
        public readonly List<Vector2> CoinRows = new List<Vector2>();
        public readonly List<Vector2> BlockRows = new List<Vector2>();
        public readonly List<Vector3> Decorations = new List<Vector3>();
        public readonly List<float> PipeXs = new List<float>();
        public string DecorationSprite;
        public float CheckpointX;
        public float KeyX;
        public float DoorX;
        public float GoalX;
        public float FireFlowerX;
        public bool HasBoss;
    }

    [MenuItem("Tools/Codex/Build Seven Midterm Levels")]
    public static void BuildAllLevels()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
        {
            throw new InvalidOperationException(
                "The source scene was not found: " + SourceScene
            );
        }

        EnsureFolder(LevelFolder);
        AssetDatabase.Refresh();

        List<string> levelPaths = new List<string>();

        for (int levelIndex = 0; levelIndex < 7; levelIndex++)
        {
            LevelPlan plan = CreatePlan(levelIndex + 1);
            string targetPath = GetLevelPath(plan.Number);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
            {
                AssetDatabase.DeleteAsset(targetPath);
            }

            if (!AssetDatabase.CopyAsset(SourceScene, targetPath))
            {
                throw new InvalidOperationException(
                    "Could not copy the source scene to " + targetPath
                );
            }

            AssetDatabase.ImportAsset(
                targetPath,
                ImportAssetOptions.ForceSynchronousImport
            );

            Scene scene = EditorSceneManager.OpenScene(
                targetPath,
                OpenSceneMode.Single
            );
            BuildScene(scene, plan);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            levelPaths.Add(targetPath);
            CapturePreviews(plan);
        }

        EditorBuildSettings.scenes = levelPaths
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Seven midterm level scenes were built successfully.");
    }

    private static string GetLevelPath(int levelNumber)
    {
        return string.Format(
            "{0}/Level{1}_{2}.unity",
            LevelFolder,
            levelNumber,
            LevelNames[levelNumber - 1]
        );
    }

    private static void BuildScene(Scene scene, LevelPlan plan)
    {
        GameObject levelRoot = GameObject.Find("Level0_Generated");

        if (levelRoot == null)
        {
            throw new InvalidOperationException(
                "Level0_Generated was not found in the source scene."
            );
        }

        levelRoot.name = "Level" + plan.Number + "_Generated";
        PreserveCommonObjects(levelRoot, plan);

        GameObject backgroundRoot = CreateGroup(
            "Background_" + plan.DisplayName,
            levelRoot.transform
        );
        GameObject terrainRoot = CreateGroup(
            "Terrain_" + plan.DisplayName,
            levelRoot.transform
        );
        GameObject obstacleRoot = CreateGroup(
            "Obstacles_" + plan.DisplayName,
            levelRoot.transform
        );
        GameObject enemyRoot = CreateGroup(
            "Enemies_" + plan.DisplayName,
            levelRoot.transform
        );
        GameObject progressionRoot = CreateGroup(
            "Progression_" + plan.DisplayName,
            levelRoot.transform
        );

        BuildBackground(plan, backgroundRoot.transform);
        BuildTerrain(plan, terrainRoot.transform);
        BuildPlatforms(plan, obstacleRoot.transform);
        BuildCoinsAndBlocks(plan, obstacleRoot.transform);
        BuildHazards(plan, obstacleRoot.transform);
        BuildDecorations(plan, obstacleRoot.transform);
        BuildGuideSpecificStructures(plan, obstacleRoot.transform);
        BuildEnemies(plan, enemyRoot.transform, scene);
        BuildProgression(plan, progressionRoot.transform);
    }

    private static void PreserveCommonObjects(
        GameObject levelRoot,
        LevelPlan plan
    )
    {
        HashSet<string> preserved = new HashSet<string>
        {
            "Player",
            "Fall_Respawn_Zone",
            "Level0_UI"
        };

        List<GameObject> children = new List<GameObject>();

        foreach (Transform child in levelRoot.transform)
        {
            children.Add(child.gameObject);
        }

        foreach (GameObject child in children)
        {
            if (!preserved.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child);
            }
        }

        GameObject player = FindChild(levelRoot.transform, "Player");

        if (player == null)
        {
            throw new InvalidOperationException("The source player was not found.");
        }

        player.transform.position = new Vector3(
            PlayerStartX,
            FloorY + 0.58f,
            0f
        );

        GameObject ui = FindChild(levelRoot.transform, "Level0_UI");

        if (ui != null)
        {
            ui.name = "Level" + plan.Number + "_UI";
        }

        GameObject fallZone = FindChild(
            levelRoot.transform,
            "Fall_Respawn_Zone"
        );

        if (fallZone != null)
        {
            fallZone.transform.position = new Vector3(50f, -8f, 0f);
            BoxCollider2D zoneCollider =
                fallZone.GetComponent<BoxCollider2D>();

            if (zoneCollider != null)
            {
                zoneCollider.size = new Vector2(140f, 2f);
            }

            FallRespawnZone respawn =
                fallZone.GetComponent<FallRespawnZone>();

            if (respawn != null)
            {
                SerializedObject serializedRespawn =
                    new SerializedObject(respawn);
                SerializedProperty position =
                    serializedRespawn.FindProperty("respawnPosition");

                if (position != null)
                {
                    position.vector3Value = player.transform.position;
                    serializedRespawn.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        Camera camera = Camera.main;

        if (camera == null)
        {
            throw new InvalidOperationException("Main Camera was not found.");
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();

        if (follow == null)
        {
            follow = camera.gameObject.AddComponent<CameraFollow2D>();
        }

        follow.Configure(
            player.transform,
            -5f,
            101f,
            0.5f,
            plan.Number == 7 ? 3.5f : 2.8f
        );
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.backgroundColor = plan.CameraColor;
        camera.transform.position = new Vector3(-5f, 0.5f, -10f);
    }

    private static void BuildBackground(
        LevelPlan plan,
        Transform parent
    )
    {
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(
            plan.BackgroundPath
        );

        if (background == null)
        {
            throw new InvalidOperationException(
                "Background was not found: " + plan.BackgroundPath
            );
        }

        float width = background.bounds.size.x;
        int count = Mathf.CeilToInt((LevelEndX + 18f) / width) + 1;

        for (int index = 0; index < count; index++)
        {
            GameObject tile = CreateSpriteObject(
                "Background_" + index,
                background,
                new Vector3(index * width - 10f, 0f, 5f),
                Vector3.one,
                parent,
                -100
            );
            tile.GetComponent<SpriteRenderer>().flipX = index % 2 == 1;
        }

        if (plan.Number == 7)
        {
            Sprite cloud = LoadRawSprite("Backgrounds", "Backgrounds_001");

            for (int index = 0; index < 12; index++)
            {
                CreateSpriteObject(
                    "Sky_Cloud_" + index,
                    cloud,
                    new Vector3(index * 10f - 5f, 2.4f + index % 3, 3f),
                    new Vector3(1.4f, 1.4f, 1f),
                    parent,
                    -20
                );
            }
        }
    }

    private static void BuildTerrain(LevelPlan plan, Transform parent)
    {
        Sprite ground = LoadRawSprite("Grounds", plan.GroundSprite);

        for (int index = 0; index < plan.Segments.Count; index++)
        {
            CreateGroundSegment(
                plan.Segments[index],
                ground,
                parent,
                "Ground_" + (index + 1)
            );
        }
    }

    private static void CreateGroundSegment(
        Segment segment,
        Sprite sprite,
        Transform parent,
        string name
    )
    {
        GameObject group = CreateGroup(name, parent);
        float width = sprite.bounds.size.x;
        float height = sprite.bounds.size.y;
        float step = width * 0.96f;
        int count = Mathf.Max(
            1,
            Mathf.CeilToInt((segment.End - segment.Start) / step)
        );

        for (int index = 0; index < count; index++)
        {
            float x = segment.Start + width * 0.5f + index * step;

            if (x - width * 0.5f > segment.End)
            {
                break;
            }

            GameObject tile = CreateSpriteObject(
                name + "_Tile_" + index,
                sprite,
                new Vector3(x, segment.Top - height * 0.5f, 0f),
                Vector3.one,
                group.transform,
                2
            );
            tile.layer = LayerMask.NameToLayer("Ground");
            BoxCollider2D collider = tile.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(
                sprite.bounds.size.x * 0.96f,
                sprite.bounds.size.y
            );
        }
    }

    private static void BuildPlatforms(LevelPlan plan, Transform parent)
    {
        Sprite platformSprite = LoadRawSprite(
            plan.PlatformAtlas,
            plan.PlatformSprite
        );

        for (int index = 0; index < plan.Platforms.Count; index++)
        {
            PlatformSpec spec = plan.Platforms[index];
            GameObject platform = CreateSpriteObject(
                "Platform_" + (index + 1),
                platformSprite,
                new Vector3(spec.X, spec.Y, 0f),
                new Vector3(
                    plan.PlatformScale,
                    plan.PlatformScale,
                    1f
                ),
                parent,
                4
            );
            platform.layer = LayerMask.NameToLayer("Ground");
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(
                platformSprite.bounds.size.x * 0.9f,
                Mathf.Max(0.12f, platformSprite.bounds.size.y * 0.18f)
            );
            collider.offset = new Vector2(
                platformSprite.bounds.center.x,
                platformSprite.bounds.max.y - collider.size.y * 0.5f
            );

            if (spec.Moving)
            {
                Rigidbody2D body = platform.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                PlatformMovement movement =
                    platform.AddComponent<PlatformMovement>();
                movement.movementRange = 1.7f;
                movement.step = index % 2 == 0 ? 0.018f : -0.018f;
            }
        }

        Sprite spring = LoadRawSprite("Objects", "Objects_002");
        Vector2 springPosition = GetSpringPosition(plan.Number);
        CreateSpriteObject(
            "Spring",
            spring,
            new Vector3(springPosition.x, springPosition.y, 0f),
            Vector3.one,
            parent,
            7
        );

        Sprite pipe = LoadRawSprite("Grounds", "Grounds_070");

        for (int index = 0; index < plan.PipeXs.Count; index++)
        {
            float x = plan.PipeXs[index];
            float top = FindGroundTop(plan, x);
            GameObject pipeObject = CreateSpriteObject(
                "Pipe_" + (index + 1),
                pipe,
                new Vector3(x, top + pipe.bounds.extents.y, 0f),
                Vector3.one,
                parent,
                6
            );
            pipeObject.layer = LayerMask.NameToLayer("Ground");
            BoxCollider2D pipeCollider =
                pipeObject.AddComponent<BoxCollider2D>();
            pipeCollider.size = pipe.bounds.size;
        }
    }

    private static void BuildCoinsAndBlocks(
        LevelPlan plan,
        Transform parent
    )
    {
        Sprite coin = LoadRawSprite("Pickups", "Pickups_001");
        AudioClip coinSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/coin.ogg"
        );

        for (int rowIndex = 0; rowIndex < plan.CoinRows.Count; rowIndex++)
        {
            Vector2 row = plan.CoinRows[rowIndex];

            for (int index = 0; index < 4; index++)
            {
                float arc = Mathf.Sin(index / 3f * Mathf.PI) * 0.55f;
                Vector3 requestedPosition = new Vector3(
                    row.x + index * 0.9f,
                    row.y + arc,
                    0f
                );
                Vector3 resolvedPosition = ResolveCoinPosition(
                    plan,
                    requestedPosition
                );
                GameObject coinObject = CreateSpriteObject(
                    "Coin_" + rowIndex + "_" + index,
                    coin,
                    resolvedPosition,
                    new Vector3(0.55f, 0.55f, 1f),
                    parent,
                    8
                );
                CircleCollider2D collider =
                    coinObject.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = Mathf.Min(
                    coin.bounds.extents.x,
                    coin.bounds.extents.y
                );
                CoinPickup pickup = coinObject.AddComponent<CoinPickup>();
                pickup.Configure(coinSound);
            }
        }

        Sprite brick = LoadRawSprite("Grounds", "Grounds_008");
        Sprite question = LoadRawSprite("Grounds", "Grounds_009");

        for (int rowIndex = 0; rowIndex < plan.BlockRows.Count; rowIndex++)
        {
            Vector2 row = plan.BlockRows[rowIndex];

            for (int index = 0; index < 5; index++)
            {
                Sprite sprite = index == 2 ? question : brick;
                GameObject block = CreateSpriteObject(
                    "Block_" + rowIndex + "_" + index,
                    sprite,
                    new Vector3(row.x + index * 0.72f, row.y, 0f),
                    new Vector3(0.72f, 0.72f, 1f),
                    parent,
                    5
                );
                block.layer = LayerMask.NameToLayer("Ground");
                BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
                collider.size = sprite.bounds.size;
            }
        }
    }

    private static void BuildHazards(LevelPlan plan, Transform parent)
    {
        Sprite liquid = LoadRawSprite("Grounds", plan.HazardSprite);
        Sprite spike = LoadRawSprite("Objects", "Objects_054");

        List<Segment> ordered = plan.Segments
            .OrderBy(segment => segment.Start)
            .ThenBy(segment => segment.End)
            .ToList();
        List<Vector2> merged = new List<Vector2>();

        foreach (Segment segment in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(new Vector2(segment.Start, segment.End));
                continue;
            }

            Vector2 current = merged[merged.Count - 1];

            if (segment.Start <= current.y)
            {
                current.y = Mathf.Max(current.y, segment.End);
                merged[merged.Count - 1] = current;
            }
            else
            {
                merged.Add(new Vector2(segment.Start, segment.End));
            }
        }

        for (int index = 0; index < merged.Count - 1; index++)
        {
            Vector2 left = merged[index];
            Vector2 right = merged[index + 1];
            float gap = right.x - left.y;

            if (gap < 1.25f)
            {
                continue;
            }

            float liquidWidth = liquid.bounds.size.x;
            int count = Mathf.Max(1, Mathf.CeilToInt(gap / liquidWidth));

            for (int liquidIndex = 0; liquidIndex < count; liquidIndex++)
            {
                float x = left.y + liquidWidth * 0.5f +
                    liquidIndex * liquidWidth * 0.95f;
                CreateSpriteObject(
                    "Hazard_" + index + "_" + liquidIndex,
                    liquid,
                    new Vector3(x, -3.78f, 0f),
                    Vector3.one,
                    parent,
                    -2
                );
            }
        }

        for (int index = 0; index < plan.SpikeXs.Count; index++)
        {
            float x = plan.SpikeXs[index];
            float top = FindGroundTop(plan, x);
            CreateSpriteObject(
                "Spikes_" + (index + 1),
                spike,
                new Vector3(
                    x,
                    top + spike.bounds.extents.y * 0.72f,
                    0f
                ),
                new Vector3(0.72f, 0.72f, 1f),
                parent,
                6
            );
        }
    }

    private static void BuildDecorations(
        LevelPlan plan,
        Transform parent
    )
    {
        Sprite decoration = LoadRawSprite(
            GetRawAtlasForSprite(plan.DecorationSprite),
            plan.DecorationSprite
        );

        for (int index = 0; index < plan.Decorations.Count; index++)
        {
            Vector3 requested = plan.Decorations[index];
            Vector3 position = ResolveDecorationPosition(
                plan,
                decoration,
                requested
            );
            float scale = GetDecorationScale(plan.Number);
            CreateSpriteObject(
                "Theme_Decoration_" + (index + 1),
                decoration,
                position,
                new Vector3(scale, scale, 1f),
                parent,
                GetDecorationOrder(plan.Number)
            );
        }
    }

    private static Vector3 ResolveCoinPosition(
        LevelPlan plan,
        Vector3 requested
    )
    {
        Vector3 resolved = requested;

        foreach (Vector2 row in plan.BlockRows)
        {
            float firstX = row.x - 0.42f;
            float lastX = row.x + 4f * 0.72f + 0.42f;

            if (
                resolved.x >= firstX &&
                resolved.x <= lastX &&
                Mathf.Abs(resolved.y - row.y) < 1.05f
            )
            {
                resolved.y = row.y + 1.25f;
            }
        }

        return resolved;
    }

    private static Vector3 ResolveDecorationPosition(
        LevelPlan plan,
        Sprite sprite,
        Vector3 requested
    )
    {
        float scale = GetDecorationScale(plan.Number);

        if (plan.Number == 3)
        {
            return new Vector3(requested.x, -2.25f, requested.z);
        }

        if (plan.Number == 6)
        {
            return new Vector3(requested.x, -1.1f, requested.z);
        }

        if (plan.Number == 7)
        {
            return new Vector3(requested.x, 1.7f, requested.z);
        }

        float groundTop = FindGroundTop(plan, requested.x);
        return new Vector3(
            requested.x,
            groundTop + sprite.bounds.extents.y * scale,
            requested.z
        );
    }

    private static float GetDecorationScale(int levelNumber)
    {
        switch (levelNumber)
        {
            case 1:
                return 0.72f;
            case 2:
                return 0.78f;
            case 3:
                return 0.9f;
            case 4:
                return 0.72f;
            case 5:
                return 0.8f;
            case 6:
                return 0.78f;
            default:
                return 0.68f;
        }
    }

    private static int GetDecorationOrder(int levelNumber)
    {
        return levelNumber == 3 || levelNumber == 7 ? -5 : 1;
    }

    private static void BuildGuideSpecificStructures(
        LevelPlan plan,
        Transform parent
    )
    {
        PlaceGroundedVisual(
            plan,
            "Level-end",
            "Level_end_005",
            -8.7f,
            0.62f,
            parent,
            "Start_Sign",
            4
        );

        switch (plan.Number)
        {
            case 1:
                PlaceBridge(
                    plan,
                    29.5f,
                    -2.82f,
                    1.25f,
                    parent,
                    "Rope_Bridge_Intro"
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_067",
                    7f,
                    0.72f,
                    parent,
                    "GreenFields_Tree",
                    1
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_088",
                    88f,
                    0.72f,
                    parent,
                    "Goal_Arrow",
                    4
                );
                break;
            case 2:
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_108",
                    63f,
                    0.8f,
                    parent,
                    "Cave_Crystal_A",
                    3
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_109",
                    66f,
                    0.72f,
                    parent,
                    "Cave_Crystal_B",
                    3
                );
                PlaceHangingHazard(
                    "Objects",
                    "Objects_067",
                    new Vector3(58f, 0.7f, 0f),
                    0.8f,
                    parent,
                    "Cave_Swinging_Mace"
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_086",
                    87f,
                    0.75f,
                    parent,
                    "Cave_Torch",
                    4
                );
                break;
            case 3:
                PlaceBridge(
                    plan,
                    12f,
                    -2.82f,
                    1.35f,
                    parent,
                    "Waterfall_Entry_Bridge"
                );
                PlaceVerticalRope(
                    57f,
                    -0.7f,
                    parent,
                    "Climb_Rope_Left"
                );
                PlaceVerticalRope(
                    59f,
                    -0.7f,
                    parent,
                    "Climb_Rope_Right"
                );
                PlaceRawVisual(
                    "Effects",
                    "Effects_074",
                    new Vector3(45.5f, -3.05f, 0f),
                    0.8f,
                    parent,
                    "Water_Splash_A",
                    3
                );
                PlaceRawVisual(
                    "Effects",
                    "Effects_074",
                    new Vector3(75.5f, -3.05f, 0f),
                    0.8f,
                    parent,
                    "Water_Splash_B",
                    3
                );
                break;
            case 4:
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_066",
                    new Vector3(69f, -0.55f, 0f),
                    0.9f,
                    parent,
                    "Ice_Platform_A"
                );
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_066",
                    new Vector3(73f, -0.15f, 0f),
                    0.9f,
                    parent,
                    "Ice_Platform_B"
                );
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_066",
                    new Vector3(77f, -0.55f, 0f),
                    0.9f,
                    parent,
                    "Ice_Platform_C"
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_103",
                    38f,
                    0.9f,
                    parent,
                    "Snow_Scenery",
                    1
                );
                break;
            case 5:
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_093",
                    39.5f,
                    0.82f,
                    parent,
                    "Ruins_Column_A",
                    3
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_094",
                    48f,
                    0.82f,
                    parent,
                    "Ruins_Column_B",
                    3
                );
                PlaceGroundedVisual(
                    plan,
                    "Tiles",
                    "Tiles_105",
                    72f,
                    0.78f,
                    parent,
                    "Desert_Cactus",
                    1
                );
                PlaceHangingHazard(
                    "Grounds",
                    "Grounds_100",
                    new Vector3(60f, 0.35f, 0f),
                    0.85f,
                    parent,
                    "Falling_Block_Marker"
                );
                break;
            case 6:
                PlaceGroundedVisual(
                    plan,
                    "Boss",
                    "Boss_066",
                    32f,
                    0.88f,
                    parent,
                    "Castle_Column_A",
                    3
                );
                PlaceGroundedVisual(
                    plan,
                    "Boss",
                    "Boss_067",
                    72f,
                    0.88f,
                    parent,
                    "Castle_Column_B",
                    3
                );
                PlaceHangingHazard(
                    "Objects",
                    "Objects_067",
                    new Vector3(45f, 0.8f, 0f),
                    0.85f,
                    parent,
                    "Castle_Mace_A"
                );
                PlaceHangingHazard(
                    "Objects",
                    "Objects_068",
                    new Vector3(77f, 0.9f, 0f),
                    0.85f,
                    parent,
                    "Castle_Mace_B"
                );
                PlaceGroundedVisual(
                    plan,
                    "Boss",
                    "Boss_073",
                    101f,
                    0.78f,
                    parent,
                    "Castle_Torch",
                    5
                );
                break;
            case 7:
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_038",
                    new Vector3(33f, -0.35f, 0f),
                    1f,
                    parent,
                    "Cloud_Platform_A"
                );
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_038",
                    new Vector3(46f, 0.05f, 0f),
                    1f,
                    parent,
                    "Cloud_Platform_B"
                );
                PlaceSolidVisual(
                    "Grounds",
                    "Grounds_038",
                    new Vector3(58f, -0.45f, 0f),
                    1f,
                    parent,
                    "Cloud_Platform_C"
                );
                PlaceRawVisual(
                    "Effects",
                    "Effects_066",
                    new Vector3(72f, -1.1f, 0f),
                    1.55f,
                    parent,
                    "Wind_Updraft_Marker",
                    1
                );
                break;
        }
    }

    private static GameObject PlaceRawVisual(
        string atlas,
        string spriteName,
        Vector3 position,
        float scale,
        Transform parent,
        string objectName,
        int sortingOrder
    )
    {
        Sprite sprite = LoadRawSprite(atlas, spriteName);
        return CreateSpriteObject(
            objectName,
            sprite,
            position,
            new Vector3(scale, scale, 1f),
            parent,
            sortingOrder
        );
    }

    private static GameObject PlaceGroundedVisual(
        LevelPlan plan,
        string atlas,
        string spriteName,
        float x,
        float scale,
        Transform parent,
        string objectName,
        int sortingOrder
    )
    {
        Sprite sprite = LoadRawSprite(atlas, spriteName);
        float top = FindGroundTop(plan, x);
        return CreateSpriteObject(
            objectName,
            sprite,
            new Vector3(x, top + sprite.bounds.extents.y * scale, 0f),
            new Vector3(scale, scale, 1f),
            parent,
            sortingOrder
        );
    }

    private static void PlaceBridge(
        LevelPlan plan,
        float x,
        float y,
        float scale,
        Transform parent,
        string objectName
    )
    {
        PlaceSolidVisual(
            "Tiles",
            "Tiles_047",
            new Vector3(x, y, 0f),
            scale,
            parent,
            objectName
        );
    }

    private static void PlaceVerticalRope(
        float x,
        float y,
        Transform parent,
        string objectName
    )
    {
        PlaceRawVisual(
            "Objects",
            "Objects_093",
            new Vector3(x, y, 0f),
            1.15f,
            parent,
            objectName,
            4
        );
    }

    private static void PlaceHangingHazard(
        string atlas,
        string spriteName,
        Vector3 position,
        float scale,
        Transform parent,
        string objectName
    )
    {
        PlaceRawVisual(
            atlas,
            spriteName,
            position,
            scale,
            parent,
            objectName,
            8
        );
    }

    private static GameObject PlaceSolidVisual(
        string atlas,
        string spriteName,
        Vector3 position,
        float scale,
        Transform parent,
        string objectName
    )
    {
        GameObject solid = PlaceRawVisual(
            atlas,
            spriteName,
            position,
            scale,
            parent,
            objectName,
            5
        );
        Sprite sprite = solid.GetComponent<SpriteRenderer>().sprite;
        solid.layer = LayerMask.NameToLayer("Ground");
        BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(
            sprite.bounds.size.x * 0.9f,
            Mathf.Max(0.12f, sprite.bounds.size.y * 0.22f)
        );
        collider.offset = new Vector2(
            sprite.bounds.center.x,
            sprite.bounds.max.y - collider.size.y * 0.5f
        );
        return solid;
    }

    private static void BuildEnemies(
        LevelPlan plan,
        Transform parent,
        Scene scene
    )
    {
        for (int index = 0; index < plan.Enemies.Count; index++)
        {
            EnemySpec spec = plan.Enemies[index];
            string prefabName = spec.Flying ? "Bird" : spec.Kind;
            string prefabPath = EnemyFolder + "/" + prefabName + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath
            );

            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    EnemyFolder + "/Hedgehog.prefab"
                );
            }

            GameObject enemy = PrefabUtility.InstantiatePrefab(
                prefab,
                scene
            ) as GameObject;
            enemy.name = "Enemy_" + (index + 1) + "_" + spec.Kind;
            enemy.transform.SetParent(parent);
            enemy.transform.localScale = spec.Flying
                ? new Vector3(0.55f, 0.55f, 1f)
                : new Vector3(0.62f, 0.62f, 1f);

            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            Animator animator = enemy.GetComponent<Animator>();

            if (animator != null)
            {
                animator.enabled = false;
            }

            if (renderer != null)
            {
                renderer.sprite = LoadRawSprite("Enemies", spec.SpriteName);
                renderer.sortingOrder = 9;
            }

            float y = spec.Flying
                ? spec.Y
                : FindGroundTop(plan, spec.X) + 0.48f;
            enemy.transform.position = new Vector3(spec.X, y, 0f);

            BoxCollider2D collider = enemy.GetComponent<BoxCollider2D>();

            if (collider != null && renderer != null && renderer.sprite != null)
            {
                collider.size = new Vector2(
                    renderer.sprite.bounds.size.x * 0.72f,
                    renderer.sprite.bounds.size.y * 0.72f
                );
                collider.offset = Vector2.zero;
            }
        }
    }

    private static void BuildProgression(
        LevelPlan plan,
        Transform parent
    )
    {
        Sprite fireFlower = LoadRawSprite("Pickups", "Pickups_020");
        float flowerGround = FindGroundTop(plan, plan.FireFlowerX);
        CreateSpriteObject(
            "FireFlower_PowerUp_Placement",
            fireFlower,
            new Vector3(
                plan.FireFlowerX,
                flowerGround + fireFlower.bounds.extents.y,
                0f
            ),
            new Vector3(0.72f, 0.72f, 1f),
            parent,
            10
        );

        Sprite checkpoint = LoadRawSprite("Objects", "Objects_066");
        float checkpointGround = FindGroundTop(plan, plan.CheckpointX);
        CreateSpriteObject(
            "Checkpoint_Placement",
            checkpoint,
            new Vector3(
                plan.CheckpointX,
                checkpointGround + checkpoint.bounds.extents.y,
                0f
            ),
            new Vector3(0.82f, 0.82f, 1f),
            parent,
            9
        );

        Sprite key = LoadRawSprite("Pickups", "Pickups_036");
        CreateSpriteObject(
            "Key_Placement",
            key,
            new Vector3(plan.KeyX, FindGroundTop(plan, plan.KeyX) + 1.35f, 0f),
            new Vector3(0.65f, 0.65f, 1f),
            parent,
            10
        );

        Sprite door = LoadRawSprite("Pickups", "Pickups_045");
        float doorGround = FindGroundTop(plan, plan.DoorX);
        CreateSpriteObject(
            "LockedDoor_Placement",
            door,
            new Vector3(
                plan.DoorX,
                doorGround + door.bounds.extents.y,
                0f
            ),
            new Vector3(0.9f, 0.9f, 1f),
            parent,
            8
        );

        if (plan.HasBoss)
        {
            Sprite boss = LoadRawSprite("Boss", "Boss_001");
            float bossX = 99.2f;
            float bossGround = FindGroundTop(plan, bossX);
            CreateSpriteObject(
                "Boss_Placement",
                boss,
                new Vector3(
                    bossX,
                    bossGround + boss.bounds.extents.y * 0.9f,
                    0f
                ),
                new Vector3(0.9f, 0.9f, 1f),
                parent,
                12
            );

            GameObject leftMarker = CreateGroup(
                "BossArena_Left_x96",
                parent
            );
            leftMarker.transform.position = new Vector3(96f, FloorY, 0f);
            GameObject rightMarker = CreateGroup(
                "BossArena_Right_x104",
                parent
            );
            rightMarker.transform.position = new Vector3(104f, FloorY, 0f);
        }

        Sprite goalSprite = LoadRawSprite("Objects", "Objects_065");
        float goalGround = FindGroundTop(plan, plan.GoalX);
        GameObject goal = CreateSpriteObject(
            "Level_Goal",
            goalSprite,
            new Vector3(
                plan.GoalX,
                goalGround + goalSprite.bounds.extents.y,
                0f
            ),
            Vector3.one,
            parent,
            11
        );
        BoxCollider2D goalCollider = goal.AddComponent<BoxCollider2D>();
        goalCollider.isTrigger = true;
        goalCollider.size = goalSprite.bounds.size;
        LevelGoal levelGoal = goal.AddComponent<LevelGoal>();
        levelGoal.Configure(
            AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/levelclear.mp3"
            )
        );
        AudioSource audio = goal.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        GameObject startMarker = CreateGroup("START_x-7", parent);
        startMarker.transform.position = new Vector3(PlayerStartX, FloorY, 0f);
    }

    private static LevelPlan CreatePlan(int levelNumber)
    {
        LevelPlan plan = new LevelPlan
        {
            Number = levelNumber,
            DisplayName = LevelNames[levelNumber - 1],
            GoalX = 107f,
            DoorX = 102.5f,
            KeyX = 91f,
            CheckpointX = 67f,
            FireFlowerX = 22f,
            HazardSprite = "Grounds_080",
            CameraColor = new Color(0.28f, 0.66f, 0.88f, 1f)
        };

        switch (levelNumber)
        {
            case 1:
                ConfigureLevel1(plan);
                break;
            case 2:
                ConfigureLevel2(plan);
                break;
            case 3:
                ConfigureLevel3(plan);
                break;
            case 4:
                ConfigureLevel4(plan);
                break;
            case 5:
                ConfigureLevel5(plan);
                break;
            case 6:
                ConfigureLevel6(plan);
                break;
            case 7:
                ConfigureLevel7(plan);
                break;
            default:
                throw new ArgumentOutOfRangeException("levelNumber");
        }

        return plan;
    }

    private static void ConfigureLevel1(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_desert1.png";
        plan.GroundSprite = "Grounds_033";
        plan.PlatformAtlas = "Grounds";
        plan.PlatformSprite = "Grounds_053";
        plan.PlatformScale = 0.95f;
        plan.DecorationSprite = "Backgrounds_037";
        plan.CheckpointX = 74f;
        plan.FireFlowerX = 22f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 28f, FloorY),
            new Segment(31.5f, 46f, FloorY),
            new Segment(52f, 67f, FloorY),
            new Segment(70.5f, 83f, FloorY),
            new Segment(87f, 99f, FloorY),
            new Segment(102f, 111f, FloorY),
            new Segment(31.5f, 41f, -1.0f),
            new Segment(74f, 80f, -1.55f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(49f, -1.75f),
            new PlatformSpec(68.7f, -1.55f),
            new PlatformSpec(85f, -1.35f),
            new PlatformSpec(100.5f, -1.8f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Hedgehog", "Enemies_007", 12f, 0f),
            new EnemySpec("Hedgehog", "Enemies_025", 38f, 0f),
            new EnemySpec("Bird", "Enemies_019", 61f, 0.5f, true),
            new EnemySpec("Spider", "Enemies_007", 91f, 0f)
        });
        plan.SpikeXs.AddRange(new[] { 81f, 89f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.7f),
            new Vector2(27.8f, -1.05f),
            new Vector2(47.5f, -0.65f),
            new Vector2(66.5f, -0.55f),
            new Vector2(98.5f, -1.1f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(18f, -0.85f),
            new Vector2(56f, -0.35f),
            new Vector2(78f, -0.25f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(5f, -1.9f, 1f),
            new Vector3(44f, -1.9f, 1f),
            new Vector3(92f, -1.9f, 1f)
        });
        plan.PipeXs.AddRange(new[] { 34f, 94f });
    }

    private static void ConfigureLevel2(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_under_bonus.png";
        plan.GroundSprite = "Grounds_036";
        plan.PlatformAtlas = "Objects";
        plan.PlatformSprite = "Objects_037";
        plan.PlatformScale = 0.95f;
        plan.DecorationSprite = "Backgrounds_047";
        plan.HazardSprite = "Grounds_083";
        plan.CameraColor = new Color(0.06f, 0.17f, 0.2f, 1f);
        plan.CheckpointX = 70f;
        plan.FireFlowerX = 18f;
        plan.KeyX = 93f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 22f, FloorY),
            new Segment(26f, 38f, FloorY),
            new Segment(42f, 50f, FloorY),
            new Segment(54f, 62f, FloorY),
            new Segment(66f, 73f, FloorY),
            new Segment(77f, 85f, FloorY),
            new Segment(89f, 98f, FloorY),
            new Segment(101f, 111f, FloorY),
            new Segment(27f, 36f, -0.95f),
            new Segment(43f, 49f, -0.05f),
            new Segment(78f, 84f, -0.55f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(24f, -1.75f),
            new PlatformSpec(40f, -0.8f),
            new PlatformSpec(52f, -1.3f),
            new PlatformSpec(64f, -0.3f),
            new PlatformSpec(75f, -1.0f),
            new PlatformSpec(87f, -0.25f),
            new PlatformSpec(99.5f, -1.45f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Spider", "Enemies_056", 11f, 0f),
            new EnemySpec("Bird", "Enemies_019", 27.5f, 0.7f, true),
            new EnemySpec("Spider", "Enemies_056", 47f, 0f),
            new EnemySpec("Bird", "Enemies_058", 70f, 0.8f, true),
            new EnemySpec("Hedgehog", "Enemies_025", 82f, 0f)
        });
        plan.SpikeXs.AddRange(new[] { 55.5f, 58f, 79f, 96f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-1f, -1.6f),
            new Vector2(21.5f, -0.8f),
            new Vector2(39f, 0.15f),
            new Vector2(67f, -0.2f),
            new Vector2(91f, -1.1f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(32f, 0.45f),
            new Vector2(57f, -0.15f),
            new Vector2(84f, 0.15f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(30f, -2.2f, 1f),
            new Vector3(60f, -2.2f, 1f),
            new Vector3(95f, -2.2f, 1f)
        });
        plan.PipeXs.AddRange(new[] { 30f, 92f });
    }

    private static void ConfigureLevel3(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_desert1.png";
        plan.GroundSprite = "Grounds_033";
        plan.PlatformAtlas = "Grounds";
        plan.PlatformSprite = "Grounds_052";
        plan.PlatformScale = 1f;
        plan.DecorationSprite = "Backgrounds_031";
        plan.CheckpointX = 79f;
        plan.FireFlowerX = 14f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 18f, FloorY),
            new Segment(23f, 32f, FloorY),
            new Segment(38f, 45f, FloorY),
            new Segment(51f, 58f, FloorY),
            new Segment(64f, 71f, FloorY),
            new Segment(77f, 84f, FloorY),
            new Segment(90f, 99f, FloorY),
            new Segment(102f, 111f, FloorY),
            new Segment(24f, 31f, -1.05f),
            new Segment(52f, 57f, -0.15f),
            new Segment(92f, 98f, -1.0f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(20.5f, -1.75f),
            new PlatformSpec(35f, -0.85f),
            new PlatformSpec(48f, -1.55f),
            new PlatformSpec(61f, -0.65f),
            new PlatformSpec(74f, -1.35f),
            new PlatformSpec(87f, -0.55f),
            new PlatformSpec(100.5f, -1.45f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Hedgehog", "Enemies_007", 10f, 0f),
            new EnemySpec("Hedgehog", "Enemies_025", 27f, 0f),
            new EnemySpec("Bird", "Enemies_019", 67f, 1.0f, true),
            new EnemySpec("Bird", "Enemies_020", 92f, 0.4f, true)
        });
        plan.SpikeXs.AddRange(new[] { 68f, 70f, 96f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.5f),
            new Vector2(18f, -0.7f),
            new Vector2(34f, 0.1f),
            new Vector2(47f, -0.3f),
            new Vector2(60f, 0.2f),
            new Vector2(84f, -0.2f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(41f, 0.1f),
            new Vector2(65f, -0.35f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(20.5f, -1.9f, 1f),
            new Vector3(48f, -1.9f, 1f),
            new Vector3(74f, -1.9f, 1f)
        });
        plan.PipeXs.AddRange(new[] { 28f, 95f });
    }

    private static void ConfigureLevel4(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_desert1.png";
        plan.GroundSprite = "Grounds_034";
        plan.PlatformAtlas = "Grounds";
        plan.PlatformSprite = "Grounds_066";
        plan.PlatformScale = 0.9f;
        plan.DecorationSprite = "Grounds_109";
        plan.CameraColor = new Color(0.66f, 0.84f, 0.96f, 1f);
        plan.CheckpointX = 76f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 14f, FloorY),
            new Segment(14f, 24f, -2.55f),
            new Segment(28f, 39f, FloorY),
            new Segment(44f, 55f, -2.65f),
            new Segment(59f, 70f, FloorY),
            new Segment(75f, 86f, -2.35f),
            new Segment(91f, 111f, FloorY),
            new Segment(47f, 53f, -0.55f),
            new Segment(77f, 83f, -0.65f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(26f, -1.6f),
            new PlatformSpec(41.5f, -0.9f),
            new PlatformSpec(57f, -1.4f),
            new PlatformSpec(72.5f, -0.4f),
            new PlatformSpec(88.5f, -1.2f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Hedgehog", "Enemies_007", 22f, 0f),
            new EnemySpec("Hedgehog", "Enemies_025", 50f, 0f),
            new EnemySpec("Bird", "Enemies_019", 69f, 0.9f, true),
            new EnemySpec("Spider", "Enemies_007", 79.5f, 0f)
        });
        plan.SpikeXs.AddRange(new[] { 62f, 64f, 86f, 94f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.5f),
            new Vector2(23f, -0.5f),
            new Vector2(40f, 0.1f),
            new Vector2(70f, 0.4f),
            new Vector2(92f, -1.0f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(33f, -0.6f),
            new Vector2(58f, -0.1f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(8f, -2.0f, 1f),
            new Vector3(38f, -2.0f, 1f),
            new Vector3(68f, -2.0f, 1f),
            new Vector3(98f, -2.0f, 1f)
        });
        plan.PipeXs.Add(82f);
    }

    private static void ConfigureLevel5(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_wildwest1.png";
        plan.GroundSprite = "Grounds_035";
        plan.PlatformAtlas = "Objects";
        plan.PlatformSprite = "Objects_018";
        plan.PlatformScale = 1f;
        plan.DecorationSprite = "Tiles_096";
        plan.HazardSprite = "Grounds_083";
        plan.CameraColor = new Color(0.9f, 0.54f, 0.28f, 1f);
        plan.CheckpointX = 76f;
        plan.FireFlowerX = 26f;
        plan.KeyX = 91f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 16f, FloorY),
            new Segment(20f, 32f, FloorY),
            new Segment(36f, 49f, -2.65f),
            new Segment(53f, 64f, FloorY),
            new Segment(68f, 78f, -2.45f),
            new Segment(82f, 94f, FloorY),
            new Segment(97f, 111f, FloorY),
            new Segment(38f, 48f, -0.75f),
            new Segment(84f, 92f, -1.05f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(18f, -1.8f),
            new PlatformSpec(34f, -0.9f),
            new PlatformSpec(51f, -1.6f),
            new PlatformSpec(66f, -0.5f),
            new PlatformSpec(80f, -1.2f),
            new PlatformSpec(95.5f, -1.7f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Hedgehog", "Enemies_007", 12f, 0f),
            new EnemySpec("Spider", "Enemies_032", 44f, 0f),
            new EnemySpec("Hedgehog", "Enemies_025", 62.5f, 0f),
            new EnemySpec("Bird", "Enemies_019", 75f, 0.8f, true),
            new EnemySpec("Spider", "Enemies_007", 89f, 0f)
        });
        plan.SpikeXs.AddRange(new[] { 22f, 24f, 57f, 59f, 83f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.6f),
            new Vector2(17f, -0.9f),
            new Vector2(32f, 0.1f),
            new Vector2(64f, -0.1f),
            new Vector2(79f, -0.3f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(29f, -0.5f),
            new Vector2(55f, -0.2f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(25f, -1.7f, 1f),
            new Vector3(58f, -1.7f, 1f),
            new Vector3(90f, -1.7f, 1f)
        });
        plan.PipeXs.Add(60f);
    }

    private static void ConfigureLevel6(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_castle.png";
        plan.GroundSprite = "Grounds_036";
        plan.PlatformAtlas = "Grounds";
        plan.PlatformSprite = "Grounds_058";
        plan.PlatformScale = 0.95f;
        plan.DecorationSprite = "Boss_070";
        plan.HazardSprite = "Grounds_083";
        plan.CameraColor = new Color(0.13f, 0.05f, 0.08f, 1f);
        plan.CheckpointX = 74f;
        plan.FireFlowerX = 19.5f;
        plan.KeyX = 90f;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 11f, FloorY),
            new Segment(15f, 24f, FloorY),
            new Segment(29f, 37f, -2.55f),
            new Segment(42f, 50f, FloorY),
            new Segment(55f, 63f, -2.35f),
            new Segment(68f, 77f, FloorY),
            new Segment(82f, 91f, -2.55f),
            new Segment(96f, 111f, FloorY),
            new Segment(31f, 36f, -0.3f),
            new Segment(70f, 76f, -0.45f)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(13f, -1.4f),
            new PlatformSpec(26.5f, -0.7f),
            new PlatformSpec(39.5f, -1.5f),
            new PlatformSpec(52.5f, -0.5f),
            new PlatformSpec(65.5f, -1.3f),
            new PlatformSpec(79.5f, -0.4f),
            new PlatformSpec(93.5f, -1.1f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Hedgehog", "Enemies_007", 8f, 0f),
            new EnemySpec("Bird", "Enemies_067", 23f, 0.8f, true),
            new EnemySpec("Spider", "Enemies_056", 34f, 0f),
            new EnemySpec("Bird", "Enemies_067", 60f, 1.0f, true),
            new EnemySpec("Bird", "Enemies_058", 74f, 0.8f, true),
            new EnemySpec("Spider", "Enemies_056", 84.5f, 0f)
        });
        plan.SpikeXs.AddRange(new[] { 17f, 43f, 45f, 69f, 71f, 97f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.5f),
            new Vector2(11f, -0.5f),
            new Vector2(25f, 0.1f),
            new Vector2(52f, 0.0f),
            new Vector2(79f, 0.15f),
            new Vector2(92f, -0.4f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(35f, 0.1f),
            new Vector2(61f, -0.1f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(20f, -1.7f, 1f),
            new Vector3(48f, -1.7f, 1f),
            new Vector3(76f, -1.7f, 1f),
            new Vector3(101f, -1.7f, 1f)
        });
        plan.PipeXs.Add(88f);
    }

    private static void ConfigureLevel7(LevelPlan plan)
    {
        plan.BackgroundPath =
            "Assets/Sprites/Background/background_desert1.png";
        plan.GroundSprite = "Grounds_042";
        plan.PlatformAtlas = "Grounds";
        plan.PlatformSprite = "Grounds_038";
        plan.PlatformScale = 0.95f;
        plan.DecorationSprite = "Backgrounds_024";
        plan.CameraColor = new Color(0.42f, 0.74f, 0.95f, 1f);
        plan.CheckpointX = 86f;
        plan.FireFlowerX = 17f;
        plan.KeyX = 91f;
        plan.DoorX = 105f;
        plan.GoalX = 109f;
        plan.HasBoss = true;
        plan.Segments.AddRange(new[]
        {
            new Segment(-10f, 8f, FloorY),
            new Segment(12f, 20f, -2.45f),
            new Segment(24f, 31f, -1.55f),
            new Segment(36f, 43f, -2.65f),
            new Segment(48f, 55f, -1.25f),
            new Segment(61f, 68f, -2.25f),
            new Segment(73f, 80f, -0.85f),
            new Segment(85f, 93f, -2.0f),
            new Segment(96f, 111f, FloorY)
        });
        plan.Platforms.AddRange(new[]
        {
            new PlatformSpec(10f, -1.8f),
            new PlatformSpec(22f, -0.5f),
            new PlatformSpec(33.5f, -1.1f),
            new PlatformSpec(45.5f, -0.2f),
            new PlatformSpec(58f, -1.0f),
            new PlatformSpec(70.5f, 0.15f),
            new PlatformSpec(82.5f, -0.7f),
            new PlatformSpec(94.5f, -1.4f)
        });
        plan.Enemies.AddRange(new[]
        {
            new EnemySpec("Bird", "Enemies_019", 15f, 0.5f, true),
            new EnemySpec("Bird", "Enemies_020", 29f, 1.2f, true),
            new EnemySpec("Hedgehog", "Enemies_025", 54f, 0f),
            new EnemySpec("Bird", "Enemies_019", 66f, 0.7f, true),
            new EnemySpec("Hedgehog", "Enemies_025", 76f, 0f),
            new EnemySpec("Bird", "Enemies_020", 87.5f, 0.6f, true)
        });
        plan.SpikeXs.AddRange(new[] { 38f, 40f, 63f, 65f });
        plan.CoinRows.AddRange(new[]
        {
            new Vector2(-2f, -1.5f),
            new Vector2(9f, -0.6f),
            new Vector2(21f, 0.3f),
            new Vector2(44f, 0.6f),
            new Vector2(69f, 0.75f),
            new Vector2(83f, -0.1f)
        });
        plan.BlockRows.AddRange(new[]
        {
            new Vector2(27f, 0.65f),
            new Vector2(74f, 1.2f)
        });
        plan.Decorations.AddRange(new[]
        {
            new Vector3(18f, 1.3f, 2f),
            new Vector3(43f, 2.0f, 2f),
            new Vector3(68f, 1.4f, 2f),
            new Vector3(91f, 1.8f, 2f)
        });
        plan.PipeXs.AddRange(new[] { 51f, 89f });
    }

    private static Vector2 GetSpringPosition(int levelNumber)
    {
        switch (levelNumber)
        {
            case 1:
                return new Vector2(59f, -2.73f);
            case 2:
                return new Vector2(29f, -0.78f);
            case 3:
                return new Vector2(81f, -2.73f);
            case 4:
                return new Vector2(48f, -0.08f);
            case 5:
                return new Vector2(64f, -2.73f);
            case 6:
                return new Vector2(70f, 0.02f);
            default:
                return new Vector2(74f, -0.28f);
        }
    }

    private static float FindGroundTop(LevelPlan plan, float x)
    {
        float result = FloorY;
        bool found = false;

        foreach (Segment segment in plan.Segments)
        {
            if (x >= segment.Start && x <= segment.End)
            {
                if (!found || segment.Top > result)
                {
                    result = segment.Top;
                    found = true;
                }
            }
        }

        return result;
    }

    private static string GetRawAtlasForSprite(string spriteName)
    {
        int separator = spriteName.IndexOf('_');
        return separator > 0
            ? spriteName.Substring(0, separator)
            : spriteName;
    }

    private static Sprite LoadRawSprite(string atlasName, string spriteName)
    {
        string path = RawFolder + "/" + atlasName + ".png";

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            Sprite sprite = asset as Sprite;

            if (sprite != null && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        throw new InvalidOperationException(
            spriteName + " was not found at " + path
        );
    }

    private static GameObject CreateSpriteObject(
        string name,
        Sprite sprite,
        Vector3 position,
        Vector3 scale,
        Transform parent,
        int sortingOrder
    )
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static GameObject CreateGroup(string name, Transform parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent);
        return group;
    }

    private static GameObject FindChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(assetFolder)
            .Replace('\\', '/');
        string name = Path.GetFileName(assetFolder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void CapturePreviews(LevelPlan plan)
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        bool followEnabled = follow != null && follow.enabled;

        if (follow != null)
        {
            follow.enabled = false;
        }

        string outputDirectory = Path.GetFullPath(
            Path.Combine(
                Application.dataPath,
                "../Documentation/MidtermLevelPreviews"
            )
        );
        Directory.CreateDirectory(outputDirectory);

        Vector3 originalPosition = camera.transform.position;
        RenderTexture originalTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        GameObject ui = GameObject.Find("Level" + plan.Number + "_UI");
        bool uiActive = ui != null && ui.activeSelf;

        if (ui != null)
        {
            ui.SetActive(false);
        }

        try
        {
            float[] centers = { 0f, 25f, 50f, 75f, 100f };

            for (int index = 0; index < centers.Length; index++)
            {
                camera.transform.position = new Vector3(
                    centers[index],
                    plan.Number == 7 ? 0.9f : 0.5f,
                    -10f
                );

                RenderTexture target = new RenderTexture(960, 540, 24);
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();

                Texture2D image = new Texture2D(
                    960,
                    540,
                    TextureFormat.RGB24,
                    false
                );
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                image.Apply();
                File.WriteAllBytes(
                    Path.Combine(
                        outputDirectory,
                        "Level" + plan.Number + "_Section_" +
                            (index + 1) + ".png"
                    ),
                    image.EncodeToPNG()
                );

                UnityEngine.Object.DestroyImmediate(image);
                camera.targetTexture = originalTarget;
                RenderTexture.active = previousActive;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
        finally
        {
            camera.targetTexture = originalTarget;
            RenderTexture.active = previousActive;
            camera.transform.position = originalPosition;

            if (follow != null)
            {
                follow.enabled = followEnabled;
            }

            if (ui != null)
            {
                ui.SetActive(uiActive);
            }
        }
    }
}
