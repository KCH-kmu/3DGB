using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MidtermSevenLevelBuilder
{
    private const string SourceScene = "Assets/Scenes/MarioClone.unity";
    private const string LevelFolder = "Assets/Scenes/Levels";
    private const string RawFolder = "Assets/MidtermSprites/Raw";
    private const string EnemyFolder = "Assets/Prefabs/Enemies";
    private const float FloorY = -3.2f;
    private const float PlayerStartX = -7f;
    private const float LevelEndX = 108f;
    private const float MaximumGapReduction = 1.2f;
    private const float MinimumJumpGap = 2.4f;
    private const float PoweredPassageClearance = 2.1f;
    private const float CoinBlockClearance = 0.25f;
    private const float GroundVisualOverlap = 0.03f;
    private const float EnemyColliderFootInset = 0.04f;

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
        public float PlatformWidthScale = 1f;
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

    [MenuItem("Tools/Codex/Apply Checkpoints To Existing Levels")]
    public static void ApplyCheckpointsToExistingLevels()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;

        for (int levelIndex = 0; levelIndex < 7; levelIndex++)
        {
            LevelPlan plan = CreatePlan(levelIndex + 1);
            string levelPath = GetLevelPath(plan.Number);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levelPath) == null)
            {
                throw new InvalidOperationException(
                    "The level scene was not found: " + levelPath
                );
            }

            Scene scene = EditorSceneManager.OpenScene(
                levelPath,
                OpenSceneMode.Single
            );
            GameObject checkpointObject =
                GameObject.Find("Checkpoint_Placement");

            if (checkpointObject == null)
            {
                throw new InvalidOperationException(
                    "Checkpoint_Placement was not found in " + levelPath
                );
            }

            float checkpointGround =
                FindGroundTop(plan, plan.CheckpointX);
            ConfigureCheckpointObject(
                checkpointObject,
                plan,
                checkpointGround
            );

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(
                originalScenePath,
                OpenSceneMode.Single
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Checkpoints were applied to all seven level scenes.");
    }

    [MenuItem("Tools/Codex/Apply Required HUD Bird AI And Respawn")]
    public static void ApplyRequiredSystemsToExistingLevels()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;

        for (int levelIndex = 0; levelIndex < 7; levelIndex++)
        {
            LevelPlan plan = CreatePlan(levelIndex + 1);
            string levelPath = GetLevelPath(plan.Number);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levelPath) == null)
            {
                throw new InvalidOperationException(
                    "The level scene was not found: " + levelPath
                );
            }

            Scene scene = EditorSceneManager.OpenScene(
                levelPath,
                OpenSceneMode.Single
            );
            GameObject levelRoot = GameObject.Find(
                "Level" + plan.Number + "_Generated"
            );
            GameObject checkpointObject = GameObject.Find(
                "Checkpoint_Placement"
            );
            GameObject obstacleRoot = GameObject.Find(
                "Obstacles_" + plan.DisplayName
            );

            if (
                levelRoot == null ||
                checkpointObject == null ||
                obstacleRoot == null
            )
            {
                throw new InvalidOperationException(
                    "Required generated objects were not found in " + levelPath
                );
            }

            ConfigureRequiredHud(levelRoot, plan.Number);
            ConfigureExistingBoss(levelRoot, plan);
            RemoveUnnecessaryLevel3Structures(levelRoot, plan);
            RebuildExistingSpikeHazards(
                plan,
                obstacleRoot.transform
            );
            ConfigureBirdAttacks();
            ConfigureEnemyStompRules();
            ConfigureCheckpointObject(
                checkpointObject,
                plan,
                FindGroundTop(plan, plan.CheckpointX)
            );
            ValidateRequiredSystems(levelRoot, plan);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(
                originalScenePath,
                OpenSceneMode.Single
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Required HUD, bird AI, boss battle, spike hazards, " +
            "checkpoints, and respawn systems were applied to all seven levels."
        );
    }

    [MenuItem("Tools/Codex/Polish Existing Level Traversal")]
    public static void PolishExistingLevelTraversal()
    {
        string originalScenePath = SceneManager.GetActiveScene().path;

        for (int levelIndex = 0; levelIndex < 7; levelIndex++)
        {
            LevelPlan plan = CreatePlan(levelIndex + 1);
            string levelPath = GetLevelPath(plan.Number);

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(levelPath) == null)
            {
                throw new InvalidOperationException(
                    "The level scene was not found: " + levelPath
                );
            }

            Scene scene = EditorSceneManager.OpenScene(
                levelPath,
                OpenSceneMode.Single
            );
            GameObject terrainRoot =
                GameObject.Find("Terrain_" + plan.DisplayName);
            GameObject obstacleRoot =
                GameObject.Find("Obstacles_" + plan.DisplayName);
            GameObject enemyRoot =
                GameObject.Find("Enemies_" + plan.DisplayName);
            GameObject progressionRoot =
                GameObject.Find("Progression_" + plan.DisplayName);

            if (
                terrainRoot == null ||
                obstacleRoot == null ||
                enemyRoot == null ||
                progressionRoot == null
            )
            {
                throw new InvalidOperationException(
                    "Generated level roots were not found in " + levelPath
                );
            }

            DisableGeneratedThemeDecorations(obstacleRoot.transform);
            ApplyPoweredTerrainClearance(
                plan,
                terrainRoot.transform,
                obstacleRoot.transform
            );
            ExtendGroundForComfort(plan, terrainRoot.transform);
            Transform[] groundedRoots =
            {
                obstacleRoot.transform,
                enemyRoot.transform,
                progressionRoot.transform
            };
            AlignKnownGroundedObjects(plan, groundedRoots);
            ConfigureExistingPlatformColliders(
                plan,
                obstacleRoot.transform
            );
            ConfigureExistingBridgeColliders(obstacleRoot.transform);
            ApplyPoweredPassageClearance(plan, obstacleRoot.transform);
            Physics2D.SyncTransforms();
            ValidatePoweredTerrainClearance(
                plan,
                terrainRoot.transform
            );
            ValidatePoweredPassageClearance(
                plan,
                terrainRoot.transform,
                obstacleRoot.transform
            );
            ValidateGroundedObjectAlignment(plan, groundedRoots);
            ValidatePlatformColliders(plan, obstacleRoot.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (!string.IsNullOrEmpty(originalScenePath))
        {
            EditorSceneManager.OpenScene(
                originalScenePath,
                OpenSceneMode.Single
            );
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "All seven levels were polished for traversal and clearance."
        );
    }

    private static void DisableGeneratedThemeDecorations(
        Transform obstacleRoot
    )
    {
        foreach (
            Transform child in
            obstacleRoot.GetComponentsInChildren<Transform>(true)
        )
        {
            if (
                child != obstacleRoot &&
                child.name.StartsWith(
                    "Theme_Decoration_",
                    StringComparison.Ordinal
                ) &&
                child.gameObject.activeSelf
            )
            {
                child.gameObject.SetActive(false);
                EditorUtility.SetDirty(child.gameObject);
            }
        }
    }

    private static void ExtendGroundForComfort(
        LevelPlan plan,
        Transform terrainRoot
    )
    {
        int primarySegmentCount = GetPrimarySegmentCount(plan);

        for (int index = 0; index < primarySegmentCount; index++)
        {
            GameObject groundGroup = FindChild(
                terrainRoot,
                "Ground_" + (index + 1)
            );

            if (groundGroup == null)
            {
                continue;
            }

            ExtendGroundGroupToSegment(
                groundGroup,
                plan.Segments[index]
            );
        }
    }

    private static void ExtendGroundGroupToSegment(
        GameObject groundGroup,
        Segment segment
    )
    {
        SetTraversalExtensionsActive(groundGroup.transform, false);

        SpriteRenderer sampleRenderer = groundGroup
            .GetComponentsInChildren<SpriteRenderer>()
            .FirstOrDefault(item => !item.name.StartsWith(
                "Traversal_Extension_",
                StringComparison.Ordinal
            ));

        if (sampleRenderer == null || sampleRenderer.sprite == null)
        {
            return;
        }

        BoxCollider2D[] colliders =
            groundGroup.GetComponentsInChildren<BoxCollider2D>()
                .Where(item => !item.name.StartsWith(
                    "Traversal_Extension_",
                    StringComparison.Ordinal
                ))
                .ToArray();

        if (colliders.Length == 0)
        {
            return;
        }

        float currentMin = colliders.Min(item => item.bounds.min.x);
        float currentMax = colliders.Max(item => item.bounds.max.x);

        float colliderWidth = sampleRenderer.sprite.bounds.size.x * 0.96f;
        float minimumUsefulExtension = colliderWidth * 0.45f;

        if (currentMin - segment.Start >= minimumUsefulExtension)
        {
            CreateGroundExtension(
                groundGroup.transform,
                sampleRenderer,
                "Traversal_Extension_Left",
                segment.Start,
                currentMin,
                segment.Top
            );
        }

        if (segment.End - currentMax >= minimumUsefulExtension)
        {
            CreateGroundExtension(
                groundGroup.transform,
                sampleRenderer,
                "Traversal_Extension_Right",
                currentMax,
                segment.End,
                segment.Top
            );
        }
    }

    private static void SetTraversalExtensionsActive(
        Transform groundGroup,
        bool active
    )
    {
        foreach (
            Transform child in
            groundGroup.GetComponentsInChildren<Transform>(true)
        )
        {
            if (
                child != groundGroup &&
                child.name.StartsWith(
                    "Traversal_Extension_",
                    StringComparison.Ordinal
                )
            )
            {
                child.gameObject.SetActive(active);
                EditorUtility.SetDirty(child.gameObject);
            }
        }
    }

    private static void CreateGroundExtension(
        Transform parent,
        SpriteRenderer sampleRenderer,
        string name,
        float start,
        float end,
        float top
    )
    {
        float requestedWidth = end - start;

        if (requestedWidth <= 0.02f)
        {
            return;
        }

        GameObject extension = FindChild(parent, name);

        if (extension == null)
        {
            extension = new GameObject(name);
            extension.transform.SetParent(parent);
            extension.layer = LayerMask.NameToLayer("Ground");
            extension.AddComponent<SpriteRenderer>();
            extension.AddComponent<BoxCollider2D>();
        }

        Sprite sprite = sampleRenderer.sprite;
        float colliderWidth = sprite.bounds.size.x * 0.96f;
        bool extendsLeft = name.EndsWith(
            "Left",
            StringComparison.Ordinal
        );
        SpriteRenderer renderer = extension.GetComponent<SpriteRenderer>();
        extension.SetActive(true);
        extension.layer = LayerMask.NameToLayer("Ground");
        renderer.sprite = sprite;
        renderer.color = sampleRenderer.color;
        renderer.sharedMaterial = sampleRenderer.sharedMaterial;
        renderer.sortingLayerID = sampleRenderer.sortingLayerID;
        renderer.sortingOrder = sampleRenderer.sortingOrder;
        renderer.flipX = sampleRenderer.flipX;
        renderer.flipY = sampleRenderer.flipY;
        renderer.drawMode = SpriteDrawMode.Simple;

        extension.transform.position = new Vector3(
            extendsLeft
                ? end - colliderWidth * 0.5f
                : start + colliderWidth * 0.5f,
            top - sprite.bounds.size.y * 0.5f,
            sampleRenderer.transform.position.z
        );
        extension.transform.localScale = Vector3.one;

        BoxCollider2D collider = extension.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(
            colliderWidth,
            sprite.bounds.size.y
        );
        collider.offset = Vector2.zero;

        EditorUtility.SetDirty(extension);
        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(collider);
    }

    private static void ApplyPoweredTerrainClearance(
        LevelPlan plan,
        Transform terrainRoot,
        Transform obstacleRoot
    )
    {
        int primarySegmentCount = GetPrimarySegmentCount(plan);

        for (
            int index = primarySegmentCount;
            index < plan.Segments.Count;
            index++
        )
        {
            GameObject groundGroup = FindChild(
                terrainRoot,
                "Ground_" + (index + 1)
            );

            if (groundGroup == null)
            {
                continue;
            }

            BoxCollider2D[] colliders = groundGroup
                .GetComponentsInChildren<BoxCollider2D>();

            if (colliders.Length == 0)
            {
                continue;
            }

            float currentTop = colliders.Max(item => item.bounds.max.y);
            float deltaY = plan.Segments[index].Top - currentTop;

            if (Mathf.Abs(deltaY) <= 0.01f)
            {
                continue;
            }

            ShiftObjectsAttachedToRaisedTerrain(
                obstacleRoot,
                plan.Segments[index],
                currentTop,
                deltaY
            );
            groundGroup.transform.position += Vector3.up * deltaY;
            EditorUtility.SetDirty(groundGroup.transform);
        }
    }

    private static void ShiftObjectsAttachedToRaisedTerrain(
        Transform obstacleRoot,
        Segment segment,
        float currentTop,
        float deltaY
    )
    {
        foreach (Transform child in obstacleRoot)
        {
            string objectName = child.name;

            if (
                objectName.StartsWith("Hazard_", StringComparison.Ordinal) ||
                objectName.StartsWith("Theme_", StringComparison.Ordinal) ||
                objectName.Contains("Bird") ||
                objectName.Contains("Bridge") ||
                objectName.Contains("Rope") ||
                objectName.Contains("Mace") ||
                objectName.Contains("Splash")
            )
            {
                continue;
            }

            float x = child.position.x;

            if (
                x < segment.Start - 0.05f ||
                x > segment.End + 0.05f ||
                child.position.y < currentTop - 0.25f
            )
            {
                continue;
            }

            child.position += Vector3.up * deltaY;
            EditorUtility.SetDirty(child);
        }
    }

    private static void AlignKnownGroundedObjects(
        LevelPlan plan,
        params Transform[] roots
    )
    {
        foreach (Transform root in roots)
        {
            if (root == null)
            {
                continue;
            }

            foreach (Transform child in root)
            {
                string objectName = child.name;
                bool isGroundedEnemy = IsGroundedEnemy(objectName);

                if (!IsKnownGroundedObject(objectName))
                {
                    continue;
                }

                SpriteRenderer renderer =
                    child.GetComponentInChildren<SpriteRenderer>();

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                if (isGroundedEnemy)
                {
                    ConfigureGroundedEnemyCollider(
                        child.gameObject,
                        renderer
                    );
                }

                float groundTop = FindGroundTop(plan, child.position.x);
                float targetBottom = groundTop - GroundVisualOverlap;
                float deltaY = targetBottom - renderer.bounds.min.y;

                if (Mathf.Abs(deltaY) <= 0.005f)
                {
                    continue;
                }

                child.position += Vector3.up * deltaY;
                EditorUtility.SetDirty(child);
            }
        }
    }

    private static bool IsGroundedEnemy(string objectName)
    {
        return
            objectName.StartsWith("Enemy_", StringComparison.Ordinal) &&
            !objectName.Contains("Bird");
    }

    private static bool IsKnownGroundedObject(string objectName)
    {
        if (
            objectName == "Spring" ||
            objectName == "FireFlower_PowerUp_Placement" ||
            objectName == "Checkpoint_Placement" ||
            objectName == "LockedDoor_Placement" ||
            objectName == "Boss_Placement" ||
            objectName == "Level_Goal" ||
            objectName == "Start_Sign" ||
            objectName == "GreenFields_Tree" ||
            objectName == "Goal_Arrow" ||
            objectName == "Cave_Crystal_A" ||
            objectName == "Cave_Crystal_B" ||
            objectName == "Cave_Torch" ||
            objectName == "Snow_Scenery" ||
            objectName == "Ruins_Column_A" ||
            objectName == "Ruins_Column_B" ||
            objectName == "Desert_Cactus" ||
            objectName == "Castle_Column_A" ||
            objectName == "Castle_Column_B" ||
            objectName == "Castle_Torch" ||
            objectName.StartsWith("Pipe_", StringComparison.Ordinal) ||
            objectName.StartsWith("Spikes_", StringComparison.Ordinal)
        )
        {
            return true;
        }

        return IsGroundedEnemy(objectName);
    }

    private static void ConfigureGroundedEnemyCollider(
        GameObject enemy,
        SpriteRenderer renderer
    )
    {
        BoxCollider2D collider = enemy.GetComponent<BoxCollider2D>();

        if (collider == null || renderer.sprite == null)
        {
            return;
        }

        Sprite sprite = renderer.sprite;
        collider.size = new Vector2(
            sprite.bounds.size.x * 0.72f,
            sprite.bounds.size.y * 0.72f
        );
        collider.offset = new Vector2(
            sprite.bounds.center.x,
            sprite.bounds.min.y +
            EnemyColliderFootInset +
            collider.size.y * 0.5f
        );
        EditorUtility.SetDirty(collider);
    }

    private static void ConfigureExistingBridgeColliders(
        Transform obstacleRoot
    )
    {
        foreach (Transform child in obstacleRoot)
        {
            if (!child.name.Contains("Bridge"))
            {
                continue;
            }

            ConfigureBridgeCollider(child.gameObject);
        }
    }

    private static void ConfigureExistingPlatformColliders(
        LevelPlan plan,
        Transform obstacleRoot
    )
    {
        float targetScaleX =
            plan.PlatformScale * plan.PlatformWidthScale;

        foreach (Transform child in obstacleRoot)
        {
            if (!child.name.StartsWith("Platform_", StringComparison.Ordinal))
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();

            if (renderer == null || renderer.sprite == null || collider == null)
            {
                continue;
            }

            child.localScale = new Vector3(
                targetScaleX,
                plan.PlatformScale,
                child.localScale.z
            );
            ConfigurePlatformCollider(renderer.sprite, collider);
            EditorUtility.SetDirty(child);
            EditorUtility.SetDirty(collider);
        }
    }

    private static void ConfigurePlatformCollider(
        Sprite sprite,
        BoxCollider2D collider
    )
    {
        float colliderHeight = Mathf.Clamp(
            sprite.bounds.size.y * 0.12f,
            0.08f,
            0.14f
        );
        float deckTop =
            sprite.bounds.min.y +
            sprite.bounds.size.y * GetPlatformDeckTopRatio(sprite.name);

        collider.size = new Vector2(
            sprite.bounds.size.x * 0.9f,
            colliderHeight
        );
        collider.offset = new Vector2(
            sprite.bounds.center.x,
            deckTop - colliderHeight * 0.5f
        );
    }

    private static float GetPlatformDeckTopRatio(string spriteName)
    {
        switch (spriteName)
        {
            case "Objects_037":
                return 0.44f;
            case "Objects_018":
                return 0.57f;
            case "Grounds_058":
                return 0.59f;
            case "Grounds_038":
                return 0.94f;
            default:
                return 0.96f;
        }
    }

    private static void ConfigureBridgeCollider(GameObject bridge)
    {
        SpriteRenderer renderer = bridge.GetComponent<SpriteRenderer>();
        BoxCollider2D collider = bridge.GetComponent<BoxCollider2D>();

        if (renderer == null || renderer.sprite == null || collider == null)
        {
            return;
        }

        Sprite sprite = renderer.sprite;
        float colliderHeight = Mathf.Max(
            0.12f,
            sprite.bounds.size.y * 0.12f
        );
        float deckTop =
            sprite.bounds.min.y + sprite.bounds.size.y * 0.55f;
        collider.size = new Vector2(
            sprite.bounds.size.x * 0.72f,
            colliderHeight
        );
        collider.offset = new Vector2(
            sprite.bounds.center.x,
            deckTop - colliderHeight * 0.5f
        );
        EditorUtility.SetDirty(bridge);
        EditorUtility.SetDirty(collider);
    }

    private static void ApplyPoweredPassageClearance(
        LevelPlan plan,
        Transform obstacleRoot
    )
    {
        Transform[] allObjects =
            obstacleRoot.GetComponentsInChildren<Transform>(true);

        for (int rowIndex = 0; rowIndex < plan.BlockRows.Count; rowIndex++)
        {
            string prefix = "Block_" + rowIndex + "_";
            List<Transform> blocks = allObjects
                .Where(item => item.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal
                ))
                .ToList();

            if (blocks.Count == 0)
            {
                continue;
            }

            float desiredY = Mathf.Max(
                blocks.Max(item => item.position.y),
                plan.BlockRows[rowIndex].y
            );

            foreach (Transform block in blocks)
            {
                if (desiredY - block.position.y <= 0.01f)
                {
                    continue;
                }

                block.position = new Vector3(
                    block.position.x,
                    desiredY,
                    block.position.z
                );
                EditorUtility.SetDirty(block);
            }

            RaiseCoinsAboveBlocks(obstacleRoot, blocks);
        }
    }

    private static void RaiseCoinsAboveBlocks(
        Transform obstacleRoot,
        List<Transform> blocks
    )
    {
        List<SpriteRenderer> blockRenderers = blocks
            .Select(item => item.GetComponent<SpriteRenderer>())
            .Where(item => item != null)
            .ToList();

        if (blockRenderers.Count == 0)
        {
            return;
        }

        float left = blockRenderers.Min(item => item.bounds.min.x);
        float right = blockRenderers.Max(item => item.bounds.max.x);
        float blockTop = blockRenderers.Max(item => item.bounds.max.y);
        float blockCenter = blocks.Average(item => item.position.y);

        foreach (
            SpriteRenderer coin in
            obstacleRoot.GetComponentsInChildren<SpriteRenderer>(true)
        )
        {
            if (
                !coin.name.StartsWith("Coin_", StringComparison.Ordinal) ||
                coin.bounds.max.x < left ||
                coin.bounds.min.x > right ||
                coin.transform.position.y < blockCenter - 0.25f
            )
            {
                continue;
            }

            float requiredBottom = blockTop + CoinBlockClearance;

            if (coin.bounds.min.y < requiredBottom)
            {
                coin.transform.position += Vector3.up *
                    (requiredBottom - coin.bounds.min.y);
                EditorUtility.SetDirty(coin.transform);
            }
        }
    }

    private static void ValidatePoweredPassageClearance(
        LevelPlan plan,
        Transform terrainRoot,
        Transform obstacleRoot
    )
    {
        List<BoxCollider2D> terrainColliders = terrainRoot
            .GetComponentsInChildren<BoxCollider2D>()
            .Where(item => item.enabled && item.gameObject.activeInHierarchy)
            .ToList();
        Transform[] allObjects =
            obstacleRoot.GetComponentsInChildren<Transform>(true);

        for (int rowIndex = 0; rowIndex < plan.BlockRows.Count; rowIndex++)
        {
            string prefix = "Block_" + rowIndex + "_";
            List<SpriteRenderer> blocks = allObjects
                .Where(item => item.name.StartsWith(
                    prefix,
                    StringComparison.Ordinal
                ))
                .Select(item => item.GetComponent<SpriteRenderer>())
                .Where(item => item != null && item.enabled)
                .ToList();

            foreach (SpriteRenderer block in blocks)
            {
                float groundTop = terrainColliders
                    .Where(item =>
                        item.bounds.max.x > block.bounds.min.x &&
                        item.bounds.min.x < block.bounds.max.x &&
                        item.bounds.max.y <= block.bounds.min.y + 0.01f
                    )
                    .Select(item => item.bounds.max.y)
                    .DefaultIfEmpty(float.NegativeInfinity)
                    .Max();

                if (float.IsNegativeInfinity(groundTop))
                {
                    continue;
                }

                float clearance = block.bounds.min.y - groundTop;

                if (clearance + 0.01f < PoweredPassageClearance)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "{0} has only {1:F2} units below {2}; " +
                            "{3:F2} units are required for powered traversal.",
                            block.name,
                            clearance,
                            plan.DisplayName,
                            PoweredPassageClearance
                        )
                    );
                }
            }
        }
    }

    private static void ValidateGroundedObjectAlignment(
        LevelPlan plan,
        params Transform[] roots
    )
    {
        foreach (Transform root in roots)
        {
            if (root == null)
            {
                continue;
            }

            foreach (Transform child in root)
            {
                if (!IsKnownGroundedObject(child.name))
                {
                    continue;
                }

                SpriteRenderer renderer =
                    child.GetComponentInChildren<SpriteRenderer>();

                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                float expectedBottom =
                    FindGroundTop(plan, child.position.x) -
                    GroundVisualOverlap;
                float alignmentError = Mathf.Abs(
                    renderer.bounds.min.y - expectedBottom
                );

                if (alignmentError > 0.015f)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "{0} in {1} is {2:F3} units away from its ground contact.",
                            child.name,
                            plan.DisplayName,
                            alignmentError
                        )
                    );
                }

                if (!IsGroundedEnemy(child.name))
                {
                    continue;
                }

                BoxCollider2D collider = child.GetComponent<BoxCollider2D>();

                if (collider == null)
                {
                    continue;
                }

                float colliderBottom =
                    collider.offset.y - collider.size.y * 0.5f;
                float expectedColliderBottom =
                    renderer.sprite.bounds.min.y +
                    EnemyColliderFootInset;

                if (
                    Mathf.Abs(
                        colliderBottom - expectedColliderBottom
                    ) > 0.005f
                )
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "{0} in {1} has a collider that does not meet the sprite feet.",
                            child.name,
                            plan.DisplayName
                        )
                    );
                }
            }
        }
    }

    private static void ValidatePlatformColliders(
        LevelPlan plan,
        Transform obstacleRoot
    )
    {
        float expectedScaleX =
            plan.PlatformScale * plan.PlatformWidthScale;

        foreach (Transform child in obstacleRoot)
        {
            if (!child.name.StartsWith("Platform_", StringComparison.Ordinal))
            {
                continue;
            }

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();

            if (renderer == null || renderer.sprite == null || collider == null)
            {
                throw new InvalidOperationException(
                    child.name + " in " + plan.DisplayName +
                    " is missing its platform renderer or collider."
                );
            }

            Sprite sprite = renderer.sprite;
            float expectedTop =
                sprite.bounds.min.y +
                sprite.bounds.size.y * GetPlatformDeckTopRatio(sprite.name);
            float colliderTop =
                collider.offset.y + collider.size.y * 0.5f;

            if (
                Mathf.Abs(colliderTop - expectedTop) > 0.005f ||
                Mathf.Abs(child.localScale.x - expectedScaleX) > 0.005f
            )
            {
                throw new InvalidOperationException(
                    child.name + " in " + plan.DisplayName +
                    " does not match its visible deck."
                );
            }
        }
    }

    private static void ValidatePoweredTerrainClearance(
        LevelPlan plan,
        Transform terrainRoot
    )
    {
        int primarySegmentCount = GetPrimarySegmentCount(plan);
        List<BoxCollider2D> lowerColliders = new List<BoxCollider2D>();

        for (int index = 0; index < primarySegmentCount; index++)
        {
            GameObject group = FindChild(
                terrainRoot,
                "Ground_" + (index + 1)
            );

            if (group != null)
            {
                lowerColliders.AddRange(
                    group.GetComponentsInChildren<BoxCollider2D>()
                );
            }
        }

        for (
            int index = primarySegmentCount;
            index < plan.Segments.Count;
            index++
        )
        {
            GameObject upperGroup = FindChild(
                terrainRoot,
                "Ground_" + (index + 1)
            );

            if (upperGroup == null)
            {
                continue;
            }

            BoxCollider2D[] upperColliders = upperGroup
                .GetComponentsInChildren<BoxCollider2D>();

            if (upperColliders.Length == 0)
            {
                continue;
            }

            float upperLeft = upperColliders.Min(item => item.bounds.min.x);
            float upperRight = upperColliders.Max(item => item.bounds.max.x);
            float upperBottom = upperColliders.Min(item => item.bounds.min.y);
            float lowerTop = lowerColliders
                .Where(item =>
                    item.bounds.max.x > upperLeft &&
                    item.bounds.min.x < upperRight &&
                    item.bounds.max.y <= upperBottom + 0.01f
                )
                .Select(item => item.bounds.max.y)
                .DefaultIfEmpty(float.NegativeInfinity)
                .Max();

            if (float.IsNegativeInfinity(lowerTop))
            {
                continue;
            }

            float clearance = upperBottom - lowerTop;

            if (clearance + 0.01f < PoweredPassageClearance)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "Ground_{0} in {1} has only {2:F2} units below it; " +
                        "{3:F2} units are required for powered traversal.",
                        index + 1,
                        plan.DisplayName,
                        clearance,
                        PoweredPassageClearance
                    )
                );
            }
        }
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
        ConfigureRequiredHud(levelRoot, plan.Number);
        ConfigureExistingBoss(levelRoot, plan);
        ConfigureBirdAttacks();
        ConfigureEnemyStompRules();
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

        if (player.GetComponent<PlayerPowerUp>() == null)
        {
            player.AddComponent<PlayerPowerUp>();
        }

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
                    plan.PlatformScale * plan.PlatformWidthScale,
                    plan.PlatformScale,
                    1f
                ),
                parent,
                4
            );
            platform.layer = LayerMask.NameToLayer("Ground");
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            ConfigurePlatformCollider(platformSprite, collider);

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
        float springGround = FindGroundTop(plan, springPosition.x);
        CreateSpriteObject(
            "Spring",
            spring,
            new Vector3(
                springPosition.x,
                GetGroundedY(springGround, spring, 1f),
                0f
            ),
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
                new Vector3(x, GetGroundedY(top, pipe, 1f), 0f),
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

        BuildSpikeHazards(plan, parent);
    }

    private static void BuildSpikeHazards(
        LevelPlan plan,
        Transform parent
    )
    {
        Sprite spike = LoadRawSprite("Objects", "Objects_054");

        for (int index = 0; index < plan.SpikeXs.Count; index++)
        {
            float x = plan.SpikeXs[index];
            float top = FindGroundTop(plan, x);
            GameObject spikeObject = CreateSpriteObject(
                "Spikes_" + (index + 1),
                spike,
                new Vector3(
                    x,
                    GetGroundedY(top, spike, 0.72f),
                    0f
                ),
                new Vector3(0.72f, 0.72f, 1f),
                parent,
                6
            );
            ConfigureSpikeHazard(spikeObject);
        }
    }

    private static void RebuildExistingSpikeHazards(
        LevelPlan plan,
        Transform obstacleRoot
    )
    {
        List<GameObject> existingSpikes = new List<GameObject>();

        foreach (Transform child in obstacleRoot)
        {
            if (!child.name.StartsWith("Spikes_", StringComparison.Ordinal))
            {
                continue;
            }

            existingSpikes.Add(child.gameObject);
        }

        foreach (GameObject existingSpike in existingSpikes)
        {
            UnityEngine.Object.DestroyImmediate(existingSpike);
        }

        BuildSpikeHazards(plan, obstacleRoot);
    }

    private static void ConfigureSpikeHazard(GameObject spikeObject)
    {
        Collider2D spikeCollider = spikeObject.GetComponent<Collider2D>();

        if (spikeCollider == null)
        {
            spikeCollider = spikeObject.AddComponent<PolygonCollider2D>();
        }

        spikeCollider.isTrigger = true;

        SpikeHazard hazard = spikeObject.GetComponent<SpikeHazard>();

        if (hazard == null)
        {
            hazard = spikeObject.AddComponent<SpikeHazard>();
        }

        EditorUtility.SetDirty(spikeObject);
        EditorUtility.SetDirty(spikeCollider);
        EditorUtility.SetDirty(hazard);
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
            new Vector3(x, GetGroundedY(top, sprite, scale), 0f),
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
        GameObject bridge = PlaceRawVisual(
            "Tiles",
            "Tiles_047",
            new Vector3(x, y, 0f),
            scale,
            parent,
            objectName,
            5
        );
        bridge.layer = LayerMask.NameToLayer("Ground");
        bridge.AddComponent<BoxCollider2D>();
        ConfigureBridgeCollider(bridge);
    }

    private static float GetGroundedY(
        float groundTop,
        Sprite sprite,
        float scale
    )
    {
        return
            groundTop -
            GroundVisualOverlap -
            sprite.bounds.min.y * scale;
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

            float y = spec.Y;

            if (!spec.Flying)
            {
                float groundTop = FindGroundTop(plan, spec.X);
                y = renderer != null && renderer.sprite != null
                    ? GetGroundedY(
                        groundTop,
                        renderer.sprite,
                        enemy.transform.localScale.y
                    )
                    : groundTop + 0.48f;
            }

            enemy.transform.position = new Vector3(spec.X, y, 0f);

            BoxCollider2D collider = enemy.GetComponent<BoxCollider2D>();

            if (collider != null && renderer != null && renderer.sprite != null)
            {
                if (spec.Flying)
                {
                    collider.size = new Vector2(
                        renderer.sprite.bounds.size.x * 0.72f,
                        renderer.sprite.bounds.size.y * 0.72f
                    );
                    collider.offset = Vector2.zero;
                }
                else
                {
                    ConfigureGroundedEnemyCollider(enemy, renderer);
                }
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
        GameObject fireFlowerObject = CreateSpriteObject(
            "FireFlower_PowerUp_Placement",
            fireFlower,
            new Vector3(
                plan.FireFlowerX,
                GetGroundedY(flowerGround, fireFlower, 0.72f),
                0f
            ),
            new Vector3(0.72f, 0.72f, 1f),
            parent,
            10
        );
        CircleCollider2D flowerCollider =
            fireFlowerObject.AddComponent<CircleCollider2D>();
        flowerCollider.isTrigger = true;
        flowerCollider.radius = Mathf.Min(
            fireFlower.bounds.extents.x,
            fireFlower.bounds.extents.y
        ) * 0.85f;
        fireFlowerObject.AddComponent<FireFlowerPowerUp>();

        Sprite checkpoint = LoadRawSprite("Objects", "Objects_066");
        float checkpointGround = FindGroundTop(plan, plan.CheckpointX);
        GameObject checkpointObject = CreateSpriteObject(
            "Checkpoint_Placement",
            checkpoint,
            new Vector3(
                plan.CheckpointX,
                GetGroundedY(checkpointGround, checkpoint, 0.82f),
                0f
            ),
            new Vector3(0.82f, 0.82f, 1f),
            parent,
            9
        );
        ConfigureCheckpointObject(
            checkpointObject,
            plan,
            checkpointGround
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
                GetGroundedY(doorGround, door, 0.9f),
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
                    GetGroundedY(bossGround, boss, 0.9f),
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
                GetGroundedY(goalGround, goalSprite, 1f),
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

    private static void ConfigureRequiredHud(
        GameObject levelRoot,
        int levelNumber
    )
    {
        GameObject uiObject = FindChild(
            levelRoot.transform,
            "Level" + levelNumber + "_UI"
        );
        GameObject player = FindChild(levelRoot.transform, "Player");

        if (uiObject == null || player == null)
        {
            throw new InvalidOperationException(
                "The generated level is missing its UI or player."
            );
        }

        Text coinCountText = FindText(uiObject.transform, "Score_Text");
        Text livesText = FindText(uiObject.transform, "Lives_Text");
        Font font =
            (coinCountText != null ? coinCountText.font : null) ??
            (livesText != null ? livesText.font : null) ??
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        coinCountText = GetOrCreateHudText(
            uiObject.transform,
            "Score_Text",
            font
        );
        livesText = GetOrCreateHudText(
            uiObject.transform,
            "Lives_Text",
            font
        );
        Text totalScoreText = GetOrCreateHudText(
            uiObject.transform,
            "TotalScore_Text",
            font
        );
        Text timerText = GetOrCreateHudText(
            uiObject.transform,
            "Timer_Text",
            font
        );

        ConfigureHudTextRect(
            livesText,
            "LIVES  3",
            new Vector2(0f, 1f),
            new Vector2(24f, -105f),
            new Vector2(300f, 50f),
            TextAnchor.MiddleLeft
        );
        ConfigureHudTextRect(
            coinCountText,
            "00",
            new Vector2(0f, 1f),
            new Vector2(25f, -22f),
            new Vector2(112f, 65f),
            TextAnchor.MiddleCenter
        );
        ConfigureHudTextRect(
            totalScoreText,
            "SCORE  000000",
            new Vector2(1f, 1f),
            new Vector2(-24f, -22f),
            new Vector2(320f, 50f),
            TextAnchor.MiddleRight
        );
        ConfigureHudTextRect(
            timerText,
            "TIME  180",
            new Vector2(1f, 1f),
            new Vector2(-24f, -72f),
            new Vector2(320f, 50f),
            TextAnchor.MiddleRight
        );

        Text bossHealthText = FindText(
            uiObject.transform,
            "BossHealth_Text"
        );

        if (levelNumber == 7)
        {
            bossHealthText = GetOrCreateHudText(
                uiObject.transform,
                "BossHealth_Text",
                font
            );
            bossHealthText.fontSize = 32;
            bossHealthText.color = new Color(1f, 0.82f, 0.2f, 1f);
            ConfigureHudTextRect(
                bossHealthText,
                "BOSS  10 / 10",
                new Vector2(0.5f, 1f),
                new Vector2(0f, -78f),
                new Vector2(420f, 54f),
                TextAnchor.MiddleCenter
            );
        }
        else if (bossHealthText != null)
        {
            bossHealthText.gameObject.SetActive(false);
        }

        Transform coinIcon = uiObject
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == "Coin_Icon");

        if (coinIcon != null)
        {
            coinIcon.gameObject.SetActive(true);
            EditorUtility.SetDirty(coinIcon.gameObject);
        }

        Text obsoleteCoinsText = FindText(
            uiObject.transform,
            "Coins_Text"
        );

        if (obsoleteCoinsText != null)
        {
            obsoleteCoinsText.gameObject.SetActive(false);
            EditorUtility.SetDirty(obsoleteCoinsText.gameObject);
        }

        GameObject messagePanel = uiObject
            .GetComponentsInChildren<Transform>(true)
            .Select(item => item.gameObject)
            .FirstOrDefault(item => item.name == "Message_Panel");
        Text messageText = FindText(uiObject.transform, "Message_Text");
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        GameHUD hud = uiObject.GetComponent<GameHUD>();

        if (
            messagePanel == null ||
            messageText == null ||
            health == null ||
            hud == null
        )
        {
            throw new InvalidOperationException(
                "The generated level HUD is missing a required component."
            );
        }

        hud.Configure(
            totalScoreText,
            livesText,
            coinCountText,
            timerText,
            messagePanel,
            messageText,
            health,
            180f
        );
        hud.ConfigureBossHealth(
            levelNumber == 7 ? bossHealthText : null
        );

        EditorUtility.SetDirty(uiObject);
        EditorUtility.SetDirty(hud);
    }

    private static Text FindText(Transform parent, string objectName)
    {
        return parent
            .GetComponentsInChildren<Text>(true)
            .FirstOrDefault(item => item.name == objectName);
    }

    private static Text GetOrCreateHudText(
        Transform parent,
        string objectName,
        Font font
    )
    {
        Text text = FindText(parent, objectName);

        if (text == null)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform)
            );
            textObject.transform.SetParent(parent, false);
            textObject.layer = LayerMask.NameToLayer("UI");
            text = textObject.AddComponent<Text>();
        }

        text.font = font;
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = text.GetComponent<Outline>();

        if (outline == null)
        {
            outline = text.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
        EditorUtility.SetDirty(text);
        EditorUtility.SetDirty(outline);
        return text;
    }

    private static void ConfigureHudTextRect(
        Text text,
        string initialText,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        TextAnchor alignment
    )
    {
        text.text = initialText;
        text.alignment = alignment;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        EditorUtility.SetDirty(rect);
    }

    private static void ConfigureBirdAttacks()
    {
        foreach (
            BirdEnemy bird in
            UnityEngine.Object.FindObjectsOfType<BirdEnemy>(true)
        )
        {
            bird.ConfigureAttack(5f, 6f, 3.5f, 1.1f, 2.4f);
            EditorUtility.SetDirty(bird);
        }
    }

    private static void ConfigureEnemyStompRules()
    {
        foreach (
            EnemyBase enemy in
            UnityEngine.Object.FindObjectsOfType<EnemyBase>(true)
        )
        {
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            bool usesGoombaSprite =
                renderer != null &&
                renderer.sprite != null &&
                renderer.sprite.name == "Enemies_007";

            if (usesGoombaSprite || enemy is BirdEnemy)
            {
                enemy.ConfigureStomp(true);
                EditorUtility.SetDirty(enemy);
            }
        }
    }

    private static void ConfigureExistingBoss(
        GameObject levelRoot,
        LevelPlan plan
    )
    {
        GameObject bossObject = FindDescendant(
            levelRoot.transform,
            "Boss_Placement"
        );

        if (!plan.HasBoss)
        {
            if (bossObject != null)
            {
                UnityEngine.Object.DestroyImmediate(bossObject);
            }

            return;
        }

        if (bossObject == null)
        {
            throw new InvalidOperationException(
                "Boss_Placement is missing in " + plan.DisplayName
            );
        }

        GameObject leftMarker = FindDescendant(
            levelRoot.transform,
            "BossArena_Left_x96"
        );
        GameObject rightMarker = FindDescendant(
            levelRoot.transform,
            "BossArena_Right_x104"
        );

        if (leftMarker == null || rightMarker == null)
        {
            throw new InvalidOperationException(
                "Boss arena markers are missing in " + plan.DisplayName
            );
        }

        Rigidbody2D bossBody = bossObject.GetComponent<Rigidbody2D>();

        if (bossBody == null)
        {
            bossBody = bossObject.AddComponent<Rigidbody2D>();
        }

        bossBody.bodyType = RigidbodyType2D.Dynamic;
        bossBody.gravityScale = 3f;
        bossBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        bossBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        bossBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        BoxCollider2D bossCollider = bossObject.GetComponent<BoxCollider2D>();

        if (bossCollider == null)
        {
            bossCollider = bossObject.AddComponent<BoxCollider2D>();
        }

        bossCollider.isTrigger = false;
        SpriteRenderer bossRenderer =
            bossObject.GetComponent<SpriteRenderer>();

        if (bossRenderer == null || bossRenderer.sprite == null)
        {
            throw new InvalidOperationException(
                "The boss sprite is missing in " + plan.DisplayName
            );
        }

        Bounds spriteBounds = bossRenderer.sprite.bounds;
        bossCollider.size = new Vector2(
            spriteBounds.size.x * 0.72f,
            spriteBounds.size.y * 0.82f
        );
        bossCollider.offset = new Vector2(
            spriteBounds.center.x,
            spriteBounds.min.y + bossCollider.size.y * 0.5f + 0.02f
        );

        BossEnemy boss = bossObject.GetComponent<BossEnemy>();

        if (boss == null)
        {
            boss = bossObject.AddComponent<BossEnemy>();
        }

        boss.Configure(
            leftMarker.transform.position.x,
            rightMarker.transform.position.x,
            10
        );

        EditorUtility.SetDirty(bossObject);
        EditorUtility.SetDirty(bossBody);
        EditorUtility.SetDirty(bossCollider);
        EditorUtility.SetDirty(boss);
    }

    private static void RemoveUnnecessaryLevel3Structures(
        GameObject levelRoot,
        LevelPlan plan
    )
    {
        if (plan.Number != 3)
        {
            return;
        }

        string[] unnecessaryNames =
        {
            "Waterfall_Entry_Bridge",
            "Climb_Rope_Left",
            "Climb_Rope_Right"
        };

        foreach (string objectName in unnecessaryNames)
        {
            GameObject structure = FindDescendant(
                levelRoot.transform,
                objectName
            );

            if (structure != null)
            {
                UnityEngine.Object.DestroyImmediate(structure);
            }
        }
    }

    private static void ValidateRequiredSystems(
        GameObject levelRoot,
        LevelPlan plan
    )
    {
        GameObject uiObject = FindChild(
            levelRoot.transform,
            "Level" + plan.Number + "_UI"
        );

        if (uiObject == null || uiObject.GetComponent<GameHUD>() == null)
        {
            throw new InvalidOperationException(
                "The required HUD is missing in " + plan.DisplayName
            );
        }

        string[] requiredTextNames =
        {
            "Lives_Text",
            "Score_Text",
            "TotalScore_Text",
            "Timer_Text"
        };

        foreach (string textName in requiredTextNames)
        {
            if (FindText(uiObject.transform, textName) == null)
            {
                throw new InvalidOperationException(
                    textName + " is missing in " + plan.DisplayName
                );
            }
        }

        BossEnemy boss = FindDescendant(
            levelRoot.transform,
            "Boss_Placement"
        )?.GetComponent<BossEnemy>();

        if (plan.HasBoss)
        {
            if (
                boss == null ||
                boss.GetComponent<Rigidbody2D>() == null ||
                boss.GetComponent<Collider2D>() == null ||
                FindText(uiObject.transform, "BossHealth_Text") == null
            )
            {
                throw new InvalidOperationException(
                    "The boss battle is not fully configured in " +
                    plan.DisplayName
                );
            }
        }
        else if (boss != null)
        {
            throw new InvalidOperationException(
                "An unexpected boss was found in " + plan.DisplayName
            );
        }

        if (plan.Number == 3)
        {
            string[] removedStructureNames =
            {
                "Waterfall_Entry_Bridge",
                "Climb_Rope_Left",
                "Climb_Rope_Right"
            };

            foreach (string objectName in removedStructureNames)
            {
                if (
                    FindDescendant(levelRoot.transform, objectName) != null
                )
                {
                    throw new InvalidOperationException(
                        objectName + " should be removed from " +
                        plan.DisplayName
                    );
                }
            }
        }

        if (
            GameObject.Find("Checkpoint_Placement")
                .GetComponent<LevelCheckpoint>() == null
        )
        {
            throw new InvalidOperationException(
                "The checkpoint is not configured in " + plan.DisplayName
            );
        }

        int plannedBirdCount = plan.Enemies.Count(item => item.Flying);
        int configuredBirdCount =
            UnityEngine.Object.FindObjectsOfType<BirdEnemy>(true).Length;

        if (configuredBirdCount < plannedBirdCount)
        {
            throw new InvalidOperationException(
                "A planned bird AI is missing in " + plan.DisplayName
            );
        }

        foreach (
            EnemyBase enemy in
            UnityEngine.Object.FindObjectsOfType<EnemyBase>(true)
        )
        {
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();
            bool mustBeStompable =
                enemy is BirdEnemy ||
                (
                    renderer != null &&
                    renderer.sprite != null &&
                    renderer.sprite.name == "Enemies_007"
                );

            if (mustBeStompable && !enemy.CanBeStomped)
            {
                throw new InvalidOperationException(
                    enemy.name + " must be stompable in " +
                    plan.DisplayName
                );
            }
        }

        ValidateSpikeHazards(plan);
    }

    private static void ValidateSpikeHazards(LevelPlan plan)
    {
        GameObject obstacleRoot = GameObject.Find(
            "Obstacles_" + plan.DisplayName
        );

        if (obstacleRoot == null)
        {
            throw new InvalidOperationException(
                "The obstacle root is missing in " + plan.DisplayName
            );
        }

        int configuredSpikeCount = 0;

        foreach (Transform child in obstacleRoot.transform)
        {
            if (!child.name.StartsWith("Spikes_", StringComparison.Ordinal))
            {
                continue;
            }

            configuredSpikeCount++;

            Collider2D spikeCollider = child.GetComponent<Collider2D>();

            if (
                spikeCollider == null ||
                !spikeCollider.isTrigger ||
                child.GetComponent<SpikeHazard>() == null
            )
            {
                throw new InvalidOperationException(
                    child.name + " has no damage trigger in " +
                    plan.DisplayName
                );
            }
        }

        if (configuredSpikeCount != plan.SpikeXs.Count)
        {
            throw new InvalidOperationException(
                "Unexpected spike count in " + plan.DisplayName +
                ": expected " + plan.SpikeXs.Count +
                ", found " + configuredSpikeCount
            );
        }
    }

    private static void ConfigureCheckpointObject(
        GameObject checkpointObject,
        LevelPlan plan,
        float checkpointGround
    )
    {
        SpriteRenderer checkpointRenderer =
            checkpointObject.GetComponent<SpriteRenderer>();
        BoxCollider2D checkpointCollider =
            checkpointObject.GetComponent<BoxCollider2D>();

        if (checkpointCollider == null)
        {
            checkpointCollider =
                checkpointObject.AddComponent<BoxCollider2D>();
        }

        checkpointCollider.isTrigger = true;

        if (
            checkpointRenderer != null &&
            checkpointRenderer.sprite != null
        )
        {
            checkpointCollider.size =
                checkpointRenderer.sprite.bounds.size;
        }

        LevelCheckpoint levelCheckpoint =
            checkpointObject.GetComponent<LevelCheckpoint>();

        if (levelCheckpoint == null)
        {
            levelCheckpoint =
                checkpointObject.AddComponent<LevelCheckpoint>();
        }

        levelCheckpoint.Configure(
            new Vector3(
                plan.CheckpointX,
                checkpointGround + 0.58f,
                0f
            )
        );

        EditorUtility.SetDirty(checkpointObject);
        EditorUtility.SetDirty(checkpointCollider);
        EditorUtility.SetDirty(levelCheckpoint);
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

        ApplyTraversalComfortAdjustments(plan);

        return plan;
    }

    private static void ApplyTraversalComfortAdjustments(LevelPlan plan)
    {
        // These generated foreground decorations frequently appeared to
        // float or overlap the playable route. Keep the scene objects
        // disabled for easy rollback and omit them from future rebuilds.
        plan.Decorations.Clear();

        int primarySegmentCount = GetPrimarySegmentCount(plan);
        Sprite groundSprite = LoadRawSprite(
            "Grounds",
            plan.GroundSprite
        );
        float naturalTileStep = Mathf.Min(
            MaximumGapReduction,
            groundSprite.bounds.size.x * 0.96f
        );

        for (int index = 0; index < primarySegmentCount - 1; index++)
        {
            Segment left = plan.Segments[index];
            Segment right = plan.Segments[index + 1];
            float gap = right.Start - left.End;

            if (gap - naturalTileStep < MinimumJumpGap)
            {
                continue;
            }

            left.End += naturalTileStep;
            plan.Segments[index] = left;
        }

        RaiseOverheadSegmentsForPoweredPlayer(plan, groundSprite);
        RaiseBlockRowsForPoweredPlayer(plan);
    }

    private static void RaiseOverheadSegmentsForPoweredPlayer(
        LevelPlan plan,
        Sprite groundSprite
    )
    {
        int primarySegmentCount = GetPrimarySegmentCount(plan);

        for (
            int index = primarySegmentCount;
            index < plan.Segments.Count;
            index++
        )
        {
            Segment upper = plan.Segments[index];
            float lowerTop = float.NegativeInfinity;

            for (
                int lowerIndex = 0;
                lowerIndex < primarySegmentCount;
                lowerIndex++
            )
            {
                Segment lower = plan.Segments[lowerIndex];

                if (
                    lower.End <= upper.Start ||
                    lower.Start >= upper.End ||
                    lower.Top >= upper.Top
                )
                {
                    continue;
                }

                lowerTop = Mathf.Max(lowerTop, lower.Top);
            }

            if (float.IsNegativeInfinity(lowerTop))
            {
                continue;
            }

            float minimumTop =
                lowerTop +
                groundSprite.bounds.size.y +
                PoweredPassageClearance;

            if (upper.Top < minimumTop)
            {
                upper.Top = minimumTop;
                plan.Segments[index] = upper;
            }
        }
    }

    private static int GetPrimarySegmentCount(LevelPlan plan)
    {
        if (plan.Segments.Count == 0)
        {
            return 0;
        }

        int count = 1;

        for (int index = 1; index < plan.Segments.Count; index++)
        {
            if (
                plan.Segments[index].Start <=
                plan.Segments[index - 1].Start
            )
            {
                break;
            }

            count++;
        }

        return count;
    }

    private static void RaiseBlockRowsForPoweredPlayer(LevelPlan plan)
    {
        Sprite blockSprite = LoadRawSprite("Grounds", "Grounds_008");
        float blockHalfWidth = blockSprite.bounds.extents.x * 0.72f;
        float blockHalfHeight = blockSprite.bounds.extents.y * 0.72f;

        for (int rowIndex = 0; rowIndex < plan.BlockRows.Count; rowIndex++)
        {
            Vector2 row = plan.BlockRows[rowIndex];
            float highestGround = float.NegativeInfinity;

            for (int blockIndex = 0; blockIndex < 5; blockIndex++)
            {
                float x = row.x + blockIndex * 0.72f;

                foreach (Segment segment in plan.Segments)
                {
                    if (
                        segment.End <= x - blockHalfWidth ||
                        segment.Start >= x + blockHalfWidth
                    )
                    {
                        continue;
                    }

                    highestGround = Mathf.Max(
                        highestGround,
                        segment.Top
                    );
                }
            }

            if (float.IsNegativeInfinity(highestGround))
            {
                continue;
            }

            float minimumCenterY =
                highestGround +
                PoweredPassageClearance +
                blockHalfHeight;

            if (row.y < minimumCenterY)
            {
                row.y = minimumCenterY;
                plan.BlockRows[rowIndex] = row;
            }
        }
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
        plan.SpikeXs.AddRange(new[] { 80.8f, 82f });
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
        plan.PlatformWidthScale = 1.3f;
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
        plan.SpikeXs.AddRange(new[] { 56.2f, 57.4f });
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
        plan.SpikeXs.AddRange(new[] { 68.4f, 69.6f });
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
        plan.SpikeXs.AddRange(new[] { 22f, 23.2f, 57f, 58.2f });
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
        plan.SpikeXs.AddRange(new[] { 21.5f, 22.7f, 46.5f, 47.7f });
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

    private static bool TryFindGroundTop(
        LevelPlan plan,
        float x,
        out float top
    )
    {
        top = FloorY;
        bool found = false;

        foreach (Segment segment in plan.Segments)
        {
            if (x < segment.Start || x > segment.End)
            {
                continue;
            }

            if (!found || segment.Top > top)
            {
                top = segment.Top;
                found = true;
            }
        }

        return found;
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

    private static GameObject FindDescendant(
        Transform parent,
        string name
    )
    {
        return parent
            .GetComponentsInChildren<Transform>(true)
            .Where(item => item != parent)
            .Select(item => item.gameObject)
            .FirstOrDefault(item => item.name == name);
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
