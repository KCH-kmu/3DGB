using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CodexLevel0Builder
{
    private const string ScenePath = "Assets/Scenes/MarioClone.unity";
    private const string EnemyAnimationFolder = "Assets/Animations/Enemy";
    private const string EnemyPrefabFolder = "Assets/Prefabs/Enemies";
    private const string TerrainPrefabFolder = "Assets/Prefabs/Terrain";
    private const string GroundPrefabPath =
        TerrainPrefabFolder + "/GroundTile.prefab";
    private const string PhysicsFolder = "Assets/Physics";
    private const string PlayerPhysicsMaterialPath =
        PhysicsFolder + "/PlayerFrictionless.physicsMaterial2D";
    private const float PlayerMoveSpeed = 5f;
    private const float PlayerJumpForce = 14f;
    private const float PlayerGravityScale = 3f;

    [MenuItem("Tools/Codex/Build Level 0")]
    public static void BuildLevelZero()
    {
        EnsureFolder("Assets/Editor");
        EnsureFolder(EnemyAnimationFolder);
        EnsureFolder(EnemyPrefabFolder);
        EnsureFolder(TerrainPrefabFolder);
        EnsureFolder(PhysicsFolder);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        CreateAdditionalEnemyAssets();
        CreateGroundPrefab();
        UpdatePlayerPrefabSettings();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single
        );

        BuildLevelEnvironment(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!Application.isBatchMode)
        {
            CaptureLevelPreviews();
        }

        Debug.Log("Codex Level 0 build completed.");
    }

    [MenuItem("Tools/Codex/Apply Assignment Sounds")]
    public static void ApplyAssignmentSounds()
    {
        EnsureFolder(PhysicsFolder);
        AssetDatabase.Refresh();
        UpdatePlayerPrefabSettings();

        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single
        );

        AudioClip coinSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/coin.ogg"
        );
        AudioClip levelCompleteSound =
            AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/levelclear.mp3"
            );

        foreach (
            CoinPickup pickup in
            UnityEngine.Object.FindObjectsOfType<CoinPickup>(true)
        )
        {
            pickup.Configure(coinSound);
            EditorUtility.SetDirty(pickup);
        }

        foreach (
            LevelGoal goal in
            UnityEngine.Object.FindObjectsOfType<LevelGoal>(true)
        )
        {
            goal.Configure(levelCompleteSound);
            ConfigureTwoDimensionalAudio(goal.gameObject);
            EditorUtility.SetDirty(goal);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Assignment sounds applied.");
    }

    public static void CaptureLevelPreviewsOnly()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        CaptureLevelPreviews();
        Debug.Log("Codex Level 0 preview capture completed.");
    }

    private static void CreateAdditionalEnemyAssets()
    {
        Sprite[] hedgehogSprites = LoadSprites(
            "Assets/Sprites/Enemies/hedgehog.png",
            "hedgehog"
        );
        Sprite[] spiderSprites = LoadSprites(
            "Assets/Sprites/Enemies/spider.png",
            "spider"
        );
        Sprite[] frogSprites = LoadSprites(
            "Assets/Sprites/Enemies/frog.png",
            "frog"
        );
        Sprite[] birdSprites = LoadSprites(
            "Assets/Sprites/Enemies/bird.png",
            "bird"
        );

        AnimationClip hedgehogWalk = CreateAnimation(
            EnemyAnimationFolder + "/HedgehogWalk.anim",
            "HedgehogWalk",
            hedgehogSprites,
            new[] { 0, 1, 2 },
            8f,
            true
        );

        AnimationClip spiderWalk = CreateAnimation(
            EnemyAnimationFolder + "/SpiderWalk.anim",
            "SpiderWalk",
            spiderSprites,
            new[] { 0, 1 },
            8f,
            true
        );

        AnimationClip frogIdle = CreateAnimation(
            EnemyAnimationFolder + "/FrogIdle.anim",
            "FrogIdle",
            frogSprites,
            new[] { 0, 1 },
            4f,
            true
        );

        AnimationClip frogJump = CreateAnimation(
            EnemyAnimationFolder + "/FrogJump.anim",
            "FrogJump",
            frogSprites,
            new[] { 2, 3, 4 },
            8f,
            false
        );

        AnimationClip birdFly = CreateAnimation(
            EnemyAnimationFolder + "/BirdFly.anim",
            "BirdFly",
            birdSprites,
            new[] { 0, 1, 2, 3 },
            8f,
            true
        );

        AnimatorController hedgehogController = CreateController(
            EnemyAnimationFolder + "/Hedgehog.controller",
            new[] { hedgehogWalk },
            "HedgehogWalk"
        );
        AnimatorController spiderController = CreateController(
            EnemyAnimationFolder + "/Spider.controller",
            new[] { spiderWalk },
            "SpiderWalk"
        );
        AnimatorController frogController = CreateController(
            EnemyAnimationFolder + "/Frog.controller",
            new[] { frogIdle, frogJump },
            "FrogIdle"
        );
        AnimatorController birdController = CreateController(
            EnemyAnimationFolder + "/Bird.controller",
            new[] { birdFly },
            "BirdFly"
        );

        CreateOrUpdateEnemyPrefab(
            "Hedgehog",
            EnemyPrefabFolder + "/Hedgehog.prefab",
            hedgehogSprites[0],
            hedgehogController,
            typeof(GroundPatrolEnemy),
            new Vector3(0.45f, 0.45f, 1f),
            RigidbodyType2D.Dynamic,
            3f,
            new Vector2(0.82f, 0.58f),
            new Vector2(0f, -0.12f),
            false,
            new Dictionary<string, float>
            {
                { "moveSpeed", 1.8f },
                { "movementRange", 2.8f }
            }
        );

        CreateOrUpdateEnemyPrefab(
            "Spider",
            EnemyPrefabFolder + "/Spider.prefab",
            spiderSprites[0],
            spiderController,
            typeof(GroundPatrolEnemy),
            new Vector3(0.42f, 0.42f, 1f),
            RigidbodyType2D.Dynamic,
            3f,
            new Vector2(0.72f, 0.65f),
            new Vector2(0f, -0.1f),
            true,
            new Dictionary<string, float>
            {
                { "moveSpeed", 1.35f },
                { "movementRange", 2.2f }
            }
        );

        CreateOrUpdateEnemyPrefab(
            "Frog",
            EnemyPrefabFolder + "/Frog.prefab",
            frogSprites[0],
            frogController,
            typeof(FrogEnemy),
            new Vector3(0.38f, 0.38f, 1f),
            RigidbodyType2D.Dynamic,
            3f,
            new Vector2(0.68f, 0.64f),
            new Vector2(0f, -0.12f),
            true,
            null
        );

        CreateOrUpdateEnemyPrefab(
            "Bird",
            EnemyPrefabFolder + "/Bird.prefab",
            birdSprites[0],
            birdController,
            typeof(BirdEnemy),
            new Vector3(0.32f, 0.32f, 1f),
            RigidbodyType2D.Kinematic,
            0f,
            new Vector2(0.72f, 0.58f),
            new Vector2(0f, -0.05f),
            true,
            null
        );

        UpdateCactusPrefab();
    }

    private static void UpdateCactusPrefab()
    {
        const string prefabPath = EnemyPrefabFolder + "/Cactus.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            throw new InvalidOperationException("Cactus prefab was not found.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            root.name = "Cactus";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(root);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(root);
            renderer.sortingOrder = 10;

            BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(root);
            collider.isTrigger = false;

            if (renderer.sprite != null)
            {
                Vector2 spriteSize = renderer.sprite.bounds.size;
                collider.size = new Vector2(
                    spriteSize.x * 0.68f,
                    spriteSize.y * 0.82f
                );
                collider.offset = new Vector2(0f, -spriteSize.y * 0.08f);
            }

            CactusEnemy cactus = GetOrAddComponent<CactusEnemy>(root);
            SetEnemyCommonProperties(cactus, false, 8f, 1);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateOrUpdateEnemyPrefab(
        string objectName,
        string prefabPath,
        Sprite sprite,
        RuntimeAnimatorController controller,
        Type enemyComponentType,
        Vector3 scale,
        RigidbodyType2D bodyType,
        float gravityScale,
        Vector2 colliderSizeMultiplier,
        Vector2 colliderOffsetMultiplier,
        bool canBeStomped,
        Dictionary<string, float> floatProperties
    )
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        bool loadedPrefab = existing != null;
        GameObject root = loadedPrefab
            ? PrefabUtility.LoadPrefabContents(prefabPath)
            : new GameObject(objectName);

        try
        {
            root.name = objectName;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = scale;

            SpriteRenderer renderer = GetOrAddComponent<SpriteRenderer>(root);
            renderer.sprite = sprite;
            renderer.sortingOrder = 10;

            Animator animator = GetOrAddComponent<Animator>(root);
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            Rigidbody2D body = GetOrAddComponent<Rigidbody2D>(root);
            body.bodyType = bodyType;
            body.gravityScale = gravityScale;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;

            BoxCollider2D collider = GetOrAddComponent<BoxCollider2D>(root);
            collider.isTrigger = false;
            Vector2 spriteSize = sprite.bounds.size;
            collider.size = new Vector2(
                spriteSize.x * colliderSizeMultiplier.x,
                spriteSize.y * colliderSizeMultiplier.y
            );
            collider.offset = new Vector2(
                spriteSize.x * colliderOffsetMultiplier.x,
                spriteSize.y * colliderOffsetMultiplier.y
            );

            Component enemyComponent = root.GetComponent(enemyComponentType);

            if (enemyComponent == null)
            {
                enemyComponent = root.AddComponent(enemyComponentType);
            }

            SetEnemyCommonProperties(
                enemyComponent,
                canBeStomped,
                8f,
                1
            );

            if (floatProperties != null)
            {
                SerializedObject serializedEnemy =
                    new SerializedObject(enemyComponent);

                foreach (KeyValuePair<string, float> pair in floatProperties)
                {
                    SerializedProperty property =
                        serializedEnemy.FindProperty(pair.Key);

                    if (property != null)
                    {
                        property.floatValue = pair.Value;
                    }
                }

                serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            if (loadedPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static void SetEnemyCommonProperties(
        Component enemy,
        bool canBeStomped,
        float bounceForce,
        int bulletHitsToDefeat
    )
    {
        SerializedObject serializedEnemy = new SerializedObject(enemy);
        SerializedProperty stomp = serializedEnemy.FindProperty("canBeStomped");
        SerializedProperty bounce = serializedEnemy.FindProperty("bounceForce");
        SerializedProperty hits = serializedEnemy.FindProperty("bulletHitsToDefeat");

        if (stomp != null)
        {
            stomp.boolValue = canBeStomped;
        }

        if (bounce != null)
        {
            bounce.floatValue = bounceForce;
        }

        if (hits != null)
        {
            hits.intValue = bulletHitsToDefeat;
        }

        serializedEnemy.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateGroundPrefab()
    {
        Sprite ground = LoadSprite(
            "Assets/Sprites/Tileset/Tileset_0.png",
            "Tileset_0_ground0"
        );
        GameObject root = new GameObject("GroundTile");

        try
        {
            root.layer = LayerMask.NameToLayer("Ground");

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = ground;
            renderer.sortingOrder = 0;

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            collider.size = ground.bounds.size;

            PrefabUtility.SaveAsPrefabAsset(root, GroundPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void UpdatePlayerPrefabSettings()
    {
        const string playerPrefabPath = "Assets/Prefabs/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(playerPrefabPath);

        try
        {
            PlayerMovement movement = root.GetComponent<PlayerMovement>();

            if (movement == null)
            {
                throw new InvalidOperationException(
                    "PlayerMovement was not found on the Player prefab."
                );
            }

            SerializedObject serializedMovement =
                new SerializedObject(movement);
            serializedMovement.FindProperty("speed").floatValue =
                PlayerMoveSpeed;
            serializedMovement.FindProperty("jumpForce").floatValue =
                PlayerJumpForce;
            serializedMovement.FindProperty("jumpSound").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/Sounds/jump.ogg"
                );
            serializedMovement.ApplyModifiedPropertiesWithoutUndo();

            PlayerHealth health = root.GetComponent<PlayerHealth>();

            if (health != null)
            {
                SerializedObject serializedHealth =
                    new SerializedObject(health);
                serializedHealth.FindProperty("hitSound").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Sounds/hit.wav"
                    );
                serializedHealth.FindProperty("gameOverSound")
                    .objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Sounds/gameover_after_hit.wav"
                    );
                serializedHealth.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerShoot shoot = root.GetComponent<PlayerShoot>();

            if (shoot != null)
            {
                SerializedObject serializedShoot =
                    new SerializedObject(shoot);
                serializedShoot.FindProperty("shootSound").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/Sounds/boss7_shot.mp3"
                    );
                serializedShoot.ApplyModifiedPropertiesWithoutUndo();
            }

            ConfigureTwoDimensionalAudio(root);

            Rigidbody2D playerBody = root.GetComponent<Rigidbody2D>();

            if (playerBody != null)
            {
                playerBody.gravityScale = PlayerGravityScale;
            }

            BoxCollider2D playerCollider = root.GetComponent<BoxCollider2D>();

            if (playerCollider != null)
            {
                playerCollider.edgeRadius = 0.05f;
                playerCollider.sharedMaterial =
                    CreatePlayerPhysicsMaterial();
            }

            PrefabUtility.SaveAsPrefabAsset(root, playerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildLevelEnvironment(Scene scene)
    {
        ClearTestHierarchy(scene);

        GameObject levelRoot = new GameObject("Level0_Generated");
        GameObject backgroundRoot = CreateGroup("Background_Desert1", levelRoot);
        GameObject terrainRoot = CreateGroup("Ground_Terrain", levelRoot);
        GameObject obstacleRoot = CreateGroup("Obstacles_1-1_Style", levelRoot);
        GameObject enemyRoot = CreateGroup("Additional_Enemies", levelRoot);
        GameObject goalRoot = CreateGroup("Level_Goal", levelRoot);

        PlayerMovement player = SetupPlayerAndCamera(
            scene,
            levelRoot.transform
        );
        BuildRepeatingBackground(backgroundRoot.transform);
        BuildTerrain(terrainRoot.transform, obstacleRoot.transform);
        PlaceAdditionalEnemies(enemyRoot.transform, scene);
        PlaceGoal(goalRoot.transform);
        CreateFallZone(levelRoot.transform);
        BuildHUD(levelRoot.transform, player.GetComponent<PlayerHealth>());
    }

    private static void ClearTestHierarchy(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera rootCamera = root.GetComponent<Camera>();

            if (rootCamera != null && root.CompareTag("MainCamera"))
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void BuildRepeatingBackground(Transform parent)
    {
        Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Background/background_desert1.png"
        );

        if (background == null)
        {
            throw new InvalidOperationException("background_desert1 was not found.");
        }

        float tileWidth = background.bounds.size.x;

        for (int index = 0; index < 4; index++)
        {
            GameObject tile = CreateSpriteObject(
                "Desert1_Background_" + index,
                background,
                new Vector3(index * tileWidth, 0f, 5f),
                Vector3.one,
                parent,
                -100
            );

            SpriteRenderer renderer = tile.GetComponent<SpriteRenderer>();
            renderer.flipX = index % 2 == 1;
        }
    }

    private static void BuildTerrain(
        Transform terrainParent,
        Transform obstacleParent
    )
    {
        Sprite ground = LoadSprite(
            "Assets/Sprites/Tileset/Tileset_0.png",
            "Tileset_0_ground0"
        );
        Sprite smallBlock = LoadSprite(
            "Assets/Sprites/Tileset/Tileset_0.png",
            "Tileset_0_block0"
        );
        Sprite pipeBody = LoadSprite(
            "Assets/Sprites/Tileset/Tileset_0.png",
            "Tileset_0_blueground0"
        );
        Sprite pipeTop = LoadSprite(
            "Assets/Sprites/Tileset/Tileset_0.png",
            "Tileset_0_ground2"
        );
        Sprite bonusBlock = LoadSprite(
            "Assets/Sprites/Block/bonusblock.png",
            "bonusblock_0"
        );
        Sprite brickBlock = LoadSprite(
            "Assets/Sprites/Block/destroyblock_new.png",
            "destroyblock_new_0"
        );

        CreateGroundSection(-12f, 24f, ground, terrainParent, "Ground_A");
        CreateGroundSection(28f, 48f, ground, terrainParent, "Ground_B");
        CreateGroundSection(52f, 76f, ground, terrainParent, "Ground_C");
        CreateGroundSection(80f, 108f, ground, terrainParent, "Ground_D");

        CreateBlockRow(13f, -1.4f, 3, bonusBlock, 0.4f, obstacleParent, "BonusRow_A");
        CreateBlockRow(36f, -0.9f, 4, brickBlock, 0.4f, obstacleParent, "BrickRow_B");
        CreateBlockRow(62f, -1.2f, 5, bonusBlock, 0.4f, obstacleParent, "BonusRow_C");

        CreatePipe(20f, 1, pipeBody, pipeTop, obstacleParent, "Pipe_A");
        CreatePipe(33f, 2, pipeBody, pipeTop, obstacleParent, "Pipe_B");
        CreatePipe(57f, 1, pipeBody, pipeTop, obstacleParent, "Pipe_C");
        CreatePipe(84f, 2, pipeBody, pipeTop, obstacleParent, "Pipe_D");

        CreateStaircase(71f, 4, smallBlock, obstacleParent, "Stairs_BeforePit");
        CreateStaircase(90f, 6, smallBlock, obstacleParent, "Stairs_ToGoal");

        PlaceMovingPlatform(50f, -2.2f, obstacleParent, "MovingPlatform_Pit1");
        PlaceMovingPlatform(78f, -2f, obstacleParent, "MovingPlatform_Pit2");

        PlaceWater(26f, obstacleParent, "Water_Pit1");
        PlaceWater(50f, obstacleParent, "Water_Pit2");
        PlaceWater(78f, obstacleParent, "Water_Pit3");

        PlaceCoinArc(
            new Vector2(24.8f, -1.5f),
            5,
            0.65f,
            obstacleParent,
            "Coins_Pit1"
        );
        PlaceCoinArc(
            new Vector2(48.7f, -0.8f),
            5,
            0.65f,
            obstacleParent,
            "Coins_Pit2"
        );
        PlaceCoinArc(
            new Vector2(76.7f, -0.5f),
            5,
            0.65f,
            obstacleParent,
            "Coins_Pit3"
        );
        PlaceJumpTrajectoryCoins(
            new Vector2(93.7f, 1.8f),
            7,
            obstacleParent,
            "Coins_FinalSteps"
        );

        Sprite mushroom = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Items/mushroom.png"
        );

        if (mushroom != null)
        {
            CreateSpriteObject(
                "Mushroom_Decoration",
                mushroom,
                new Vector3(
                    10.5f,
                    -3.32f + mushroom.bounds.extents.y * 0.35f,
                    0f
                ),
                new Vector3(0.35f, 0.35f, 1f),
                obstacleParent,
                8
            );
        }
    }

    private static void PlaceAdditionalEnemies(
        Transform parent,
        Scene scene
    )
    {
        PlacePrefab(
            EnemyPrefabFolder + "/Cactus.prefab",
            new Vector3(16.5f, -2.4f, 0f),
            parent,
            scene,
            "Cactus_Level0"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Hedgehog.prefab",
            new Vector3(22f, -2.2f, 0f),
            parent,
            scene,
            "Hedgehog_Level0"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Spider.prefab",
            new Vector3(38f, -2.1f, 0f),
            parent,
            scene,
            "Spider_Level0"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Frog.prefab",
            new Vector3(44f, -2f, 0f),
            parent,
            scene,
            "Frog_Level0"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Bird.prefab",
            new Vector3(66f, 0.8f, 0f),
            parent,
            scene,
            "Bird_Level0"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Hedgehog.prefab",
            new Vector3(67.5f, -2.2f, 0f),
            parent,
            scene,
            "Hedgehog_Level0_2"
        );
        PlacePrefab(
            EnemyPrefabFolder + "/Spider.prefab",
            new Vector3(88f, -2.1f, 0f),
            parent,
            scene,
            "Spider_Level0_2"
        );
    }

    private static void PlaceGoal(Transform parent)
    {
        Sprite castle = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Items/castle_finish.png"
        );

        if (castle == null)
        {
            return;
        }

        GameObject goal = CreateSpriteObject(
            "Goal_Castle",
            castle,
            new Vector3(103f, -1.35f, 0f),
            new Vector3(0.3f, 0.3f, 1f),
            parent,
            5
        );

        BoxCollider2D collider = goal.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = castle.bounds.size;
        LevelGoal levelGoal = goal.AddComponent<LevelGoal>();
        levelGoal.Configure(
            AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Sounds/levelclear.mp3"
            )
        );
        ConfigureTwoDimensionalAudio(goal);
    }

    private static PlayerMovement SetupPlayerAndCamera(
        Scene scene,
        Transform parent
    )
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player.prefab"
        );

        if (playerPrefab == null)
        {
            throw new InvalidOperationException("Player prefab was not found.");
        }

        GameObject playerObject = PrefabUtility.InstantiatePrefab(
            playerPrefab,
            scene
        ) as GameObject;
        playerObject.name = "Player";
        playerObject.transform.SetParent(parent);
        playerObject.transform.position = new Vector3(-7f, -2.5f, 0f);

        PlayerMovement player = playerObject.GetComponent<PlayerMovement>();
        Camera camera = Camera.main;

        if (player == null || camera == null)
        {
            throw new InvalidOperationException("Player or Main Camera was not found.");
        }

        CameraFollow2D follow = GetOrAddComponent<CameraFollow2D>(camera.gameObject);
        follow.Configure(player.transform, -5f, 96f, 0f, 2.5f);

        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(-5f, 0f, -10f);
        camera.backgroundColor = new Color(0.18f, 0.55f, 0.72f, 1f);
        return player;
    }

    private static void CreateFallZone(Transform parent)
    {
        GameObject zone = new GameObject("Fall_Respawn_Zone");
        zone.transform.SetParent(parent);
        zone.transform.position = new Vector3(50f, -8f, 0f);

        BoxCollider2D collider = zone.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(125f, 2f);

        zone.AddComponent<FallRespawnZone>();
    }

    private static void BuildHUD(
        Transform parent,
        PlayerHealth playerHealth
    )
    {
        GameObject canvasObject = new GameObject(
            "Level0_UI",
            typeof(RectTransform)
        );
        canvasObject.transform.SetParent(parent);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode =
            CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        Font font = Resources.GetBuiltinResource<Font>(
            "LegacyRuntime.ttf"
        );
        Sprite coinIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/UI/game/coin.png"
        );

        if (coinIcon != null)
        {
            Image icon = CreateUIImage(
                "Coin_Icon",
                canvasObject.transform,
                coinIcon,
                new Vector2(20f, -20f),
                new Vector2(190f, 71f)
            );
            icon.preserveAspect = true;
        }

        Text scoreText = CreateUIText(
            "Score_Text",
            canvasObject.transform,
            font,
            "0",
            new Vector2(25f, -22f),
            new Vector2(112f, 65f),
            30,
            TextAnchor.MiddleCenter
        );
        Text livesText = CreateUIText(
            "Lives_Text",
            canvasObject.transform,
            font,
            "LIVES  3",
            new Vector2(24f, -105f),
            new Vector2(300f, 50f),
            30,
            TextAnchor.MiddleLeft
        );

        GameObject messagePanel = new GameObject(
            "Message_Panel",
            typeof(RectTransform)
        );
        messagePanel.transform.SetParent(canvasObject.transform);
        messagePanel.layer = LayerMask.NameToLayer("UI");
        RectTransform panelRect = messagePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(560f, 150f);

        Image panelImage = messagePanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.78f);

        Text messageText = CreateUIText(
            "Message_Text",
            messagePanel.transform,
            font,
            "LEVEL COMPLETE!",
            Vector2.zero,
            new Vector2(540f, 140f),
            48,
            TextAnchor.MiddleCenter,
            false
        );
        messageText.color = new Color(1f, 0.84f, 0.12f, 1f);

        RectTransform messageRect = messageText.rectTransform;
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        GameHUD hud = canvasObject.AddComponent<GameHUD>();
        hud.Configure(
            scoreText,
            livesText,
            messagePanel,
            messageText,
            playerHealth
        );
        messagePanel.SetActive(false);
    }

    private static Text CreateUIText(
        string objectName,
        Transform parent,
        Font font,
        string initialText,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAnchor alignment,
        bool topLeftAnchor = true
    )
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform)
        );
        textObject.transform.SetParent(parent);
        textObject.layer = LayerMask.NameToLayer("UI");

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.text = initialText;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        Vector2 anchor = topLeftAnchor
            ? new Vector2(0f, 1f)
            : new Vector2(0.5f, 0.5f);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = topLeftAnchor
            ? new Vector2(0f, 1f)
            : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static Image CreateUIImage(
        string objectName,
        Transform parent,
        Sprite sprite,
        Vector2 anchoredPosition,
        Vector2 size
    )
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform)
        );
        imageObject.transform.SetParent(parent);
        imageObject.layer = LayerMask.NameToLayer("UI");

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return image;
    }

    private static void CreateGroundSection(
        float startX,
        float endX,
        Sprite sprite,
        Transform parent,
        string groupName
    )
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);
        ConfigureCompositeGroup(group);

        float width = sprite.bounds.size.x;
        int index = 0;

        for (
            float x = startX + width * 0.5f;
            x < endX;
            x += width
        )
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                GroundPrefabPath
            );

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "GroundTile prefab was not found."
                );
            }

            GameObject tile = PrefabUtility.InstantiatePrefab(prefab)
                as GameObject;
            tile.name = groupName + "_" + index;
            tile.transform.SetParent(group.transform);
            tile.transform.position = new Vector3(x, -4f, 0f);

            BoxCollider2D tileCollider = tile.GetComponent<BoxCollider2D>();

            if (tileCollider != null)
            {
                tileCollider.usedByComposite = true;
            }

            index++;
        }
    }

    private static void CreateBlockRow(
        float startX,
        float y,
        int count,
        Sprite sprite,
        float scale,
        Transform parent,
        string groupName
    )
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);
        ConfigureCompositeGroup(group);
        float spacing = sprite.bounds.size.x * scale;

        for (int index = 0; index < count; index++)
        {
            GameObject block = CreateSolidSprite(
                groupName + "_" + index,
                sprite,
                new Vector3(startX + spacing * index, y, 0f),
                new Vector3(scale, scale, 1f),
                group.transform,
                3
            );
            block.GetComponent<BoxCollider2D>().usedByComposite = true;
        }
    }

    private static void CreatePipe(
        float x,
        int bodyRows,
        Sprite bodySprite,
        Sprite topSprite,
        Transform parent,
        string groupName
    )
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);
        ConfigureCompositeGroup(group);
        float bodySize = bodySprite.bounds.size.x;
        float groundTop = -3.32f;

        for (int row = 0; row < bodyRows; row++)
        {
            float y = groundTop + bodySize * 0.5f + row * bodySize;

            GameObject body = CreateSolidSprite(
                groupName + "_Body_" + row,
                bodySprite,
                new Vector3(x, y, 0f),
                Vector3.one,
                group.transform,
                2
            );
            body.GetComponent<BoxCollider2D>().usedByComposite = true;
        }

        float topY = groundTop + bodyRows * bodySize +
            topSprite.bounds.extents.y;

        GameObject top = CreateSolidSprite(
            groupName + "_Top",
            topSprite,
            new Vector3(x, topY, 0f),
            Vector3.one,
            group.transform,
            3
        );
        top.GetComponent<BoxCollider2D>().usedByComposite = true;
    }

    private static void CreateStaircase(
        float startX,
        int steps,
        Sprite sprite,
        Transform parent,
        string groupName
    )
    {
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);
        ConfigureCompositeGroup(group);
        float size = sprite.bounds.size.x;
        float groundTop = -3.32f;

        for (int column = 0; column < steps; column++)
        {
            for (int row = 0; row <= column; row++)
            {
                GameObject block = CreateSolidSprite(
                    groupName + "_" + column + "_" + row,
                    sprite,
                    new Vector3(
                        startX + column * size,
                        groundTop + size * 0.5f + row * size,
                        0f
                    ),
                    Vector3.one,
                    group.transform,
                    2
                );
                block.GetComponent<BoxCollider2D>().usedByComposite = true;
            }
        }
    }

    private static void ConfigureCompositeGroup(GameObject group)
    {
        group.layer = LayerMask.NameToLayer("Ground");

        Rigidbody2D body = group.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        CompositeCollider2D composite =
            group.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
    }

    private static void PlaceMovingPlatform(
        float x,
        float y,
        Transform parent,
        string instanceName
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Platforms/MovingPlatform.prefab"
        );

        if (prefab == null)
        {
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        instance.name = instanceName;
        instance.transform.SetParent(parent);
        instance.transform.position = new Vector3(x, y, 0f);
    }

    private static void PlaceWater(
        float x,
        Transform parent,
        string objectName
    )
    {
        Sprite water = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Objects/Water.png"
        );

        if (water == null)
        {
            return;
        }

        CreateSpriteObject(
            objectName,
            water,
            new Vector3(x, -4.9f, 1f),
            new Vector3(1.05f, 0.55f, 1f),
            parent,
            -2
        );
    }

    private static void PlaceCoinArc(
        Vector2 start,
        int count,
        float spacing,
        Transform parent,
        string groupName,
        float peakHeight = 0.7f
    )
    {
        Sprite coin = LoadSprite(
            "Assets/Sprites/Items/coin.png",
            "coin_0"
        );

        if (coin == null)
        {
            return;
        }

        AudioClip pickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/coin.ogg"
        );
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);

        for (int index = 0; index < count; index++)
        {
            float normalized = count <= 1
                ? 0f
                : (float)index / (count - 1);
            float arcHeight =
                Mathf.Sin(normalized * Mathf.PI) * peakHeight;
            GameObject coinObject = CreateSpriteObject(
                groupName + "_" + index,
                coin,
                new Vector3(
                    start.x + index * spacing,
                    start.y + arcHeight,
                    0f
                ),
                new Vector3(0.26f, 0.26f, 1f),
                group.transform,
                8
            );

            CircleCollider2D collider =
                coinObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.offset = coin.bounds.center;
            collider.radius = Mathf.Min(
                coin.bounds.extents.x,
                coin.bounds.extents.y
            );

            CoinPickup pickup = coinObject.AddComponent<CoinPickup>();
            pickup.Configure(pickupSound);
        }
    }

    private static void PlaceJumpTrajectoryCoins(
        Vector2 firstCoinPosition,
        int count,
        Transform parent,
        string groupName
    )
    {
        Sprite coin = LoadSprite(
            "Assets/Sprites/Items/coin.png",
            "coin_0"
        );

        if (coin == null || count <= 0)
        {
            return;
        }

        AudioClip pickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Sounds/coin.ogg"
        );
        GameObject group = new GameObject(groupName);
        group.transform.SetParent(parent);

        float gravity = Mathf.Abs(Physics2D.gravity.y) * PlayerGravityScale;
        float flightTime = 2f * PlayerJumpForce / gravity;
        float sampleInterval = count <= 1
            ? 0f
            : flightTime / (count - 1);

        for (int index = 0; index < count; index++)
        {
            float time = sampleInterval * index;
            float x = firstCoinPosition.x + PlayerMoveSpeed * time;
            float y = firstCoinPosition.y +
                PlayerJumpForce * time -
                0.5f * gravity * time * time;

            GameObject coinObject = CreateSpriteObject(
                groupName + "_" + index,
                coin,
                new Vector3(x, y, 0f),
                new Vector3(0.26f, 0.26f, 1f),
                group.transform,
                8
            );

            CircleCollider2D collider =
                coinObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.offset = coin.bounds.center;
            collider.radius = Mathf.Min(
                coin.bounds.extents.x,
                coin.bounds.extents.y
            );

            CoinPickup pickup = coinObject.AddComponent<CoinPickup>();
            pickup.Configure(pickupSound);
        }
    }

    private static void PlacePrefab(
        string prefabPath,
        Vector3 position,
        Transform parent,
        Scene scene,
        string instanceName
    )
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            throw new InvalidOperationException(prefabPath + " was not found.");
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(
            prefab,
            scene
        ) as GameObject;

        instance.name = instanceName;
        instance.transform.SetParent(parent);
        instance.transform.position = position;
    }

    private static GameObject CreateSolidSprite(
        string objectName,
        Sprite sprite,
        Vector3 position,
        Vector3 scale,
        Transform parent,
        int sortingOrder
    )
    {
        GameObject gameObject = CreateSpriteObject(
            objectName,
            sprite,
            position,
            scale,
            parent,
            sortingOrder
        );

        gameObject.layer = LayerMask.NameToLayer("Ground");
        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.size = sprite.bounds.size;
        return gameObject;
    }

    private static GameObject CreateSpriteObject(
        string objectName,
        Sprite sprite,
        Vector3 position,
        Vector3 scale,
        Transform parent,
        int sortingOrder
    )
    {
        GameObject gameObject = new GameObject(objectName);
        gameObject.transform.SetParent(parent);
        gameObject.transform.position = position;
        gameObject.transform.localScale = scale;

        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return gameObject;
    }

    private static GameObject CreateGroup(string name, GameObject parent)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent.transform);
        return group;
    }

    private static Sprite[] LoadSprites(string assetPath, string prefix)
    {
        List<Sprite> sprites = new List<Sprite>();

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Sprite sprite = asset as Sprite;

            if (sprite != null && sprite.name.StartsWith(prefix + "_"))
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((left, right) =>
            ExtractSpriteIndex(left.name).CompareTo(
                ExtractSpriteIndex(right.name)
            )
        );

        if (sprites.Count == 0)
        {
            throw new InvalidOperationException("No sprites found at " + assetPath);
        }

        return sprites.ToArray();
    }

    private static int ExtractSpriteIndex(string spriteName)
    {
        int separator = spriteName.LastIndexOf('_');
        int index;
        return separator >= 0 &&
            int.TryParse(spriteName.Substring(separator + 1), out index)
                ? index
                : 0;
    }

    private static Sprite LoadSprite(string assetPath, string spriteName)
    {
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Sprite sprite = asset as Sprite;

            if (sprite != null && sprite.name == spriteName)
            {
                return sprite;
            }
        }

        throw new InvalidOperationException(
            spriteName + " was not found at " + assetPath
        );
    }

    private static AnimationClip CreateAnimation(
        string assetPath,
        string clipName,
        Sprite[] sprites,
        int[] frameIndices,
        float frameRate,
        bool loop
    )
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);

        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, assetPath);
        }

        clip.name = clipName;
        clip.frameRate = frameRate;

        ObjectReferenceKeyframe[] keyframes =
            new ObjectReferenceKeyframe[frameIndices.Length];

        for (int index = 0; index < frameIndices.Length; index++)
        {
            keyframes[index] = new ObjectReferenceKeyframe
            {
                time = index / frameRate,
                value = sprites[frameIndices[index]]
            };
        }

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(
            string.Empty,
            typeof(SpriteRenderer),
            "m_Sprite"
        );

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateController(
        string assetPath,
        AnimationClip[] clips,
        string defaultStateName
    )
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(
                assetPath
            );
        }

        AnimatorStateMachine stateMachine =
            controller.layers[0].stateMachine;
        AnimatorState defaultState = null;

        foreach (AnimationClip clip in clips)
        {
            AnimatorState state = FindState(stateMachine, clip.name);

            if (state == null)
            {
                state = stateMachine.AddState(clip.name);
            }

            state.motion = clip;

            if (clip.name == defaultStateName)
            {
                defaultState = state;
            }
        }

        if (defaultState != null)
        {
            stateMachine.defaultState = defaultState;
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState FindState(
        AnimatorStateMachine stateMachine,
        string stateName
    )
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject)
        where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static AudioSource ConfigureTwoDimensionalAudio(
        GameObject gameObject
    )
    {
        AudioSource source = GetOrAddComponent<AudioSource>(gameObject);
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static PhysicsMaterial2D CreatePlayerPhysicsMaterial()
    {
        PhysicsMaterial2D material =
            AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(
                PlayerPhysicsMaterialPath
            );

        if (material == null)
        {
            material = new PhysicsMaterial2D("PlayerFrictionless");
            AssetDatabase.CreateAsset(material, PlayerPhysicsMaterialPath);
        }

        material.friction = 0f;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        return material;
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

    private static void CaptureLevelPreviews()
    {
        Camera camera = Camera.main;

        if (camera == null)
        {
            return;
        }

        CameraFollow2D follow = camera.GetComponent<CameraFollow2D>();
        bool followWasEnabled = follow != null && follow.enabled;

        if (follow != null)
        {
            follow.enabled = false;
        }

        string outputDirectory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "../CodexLevel0Previews")
        );
        Directory.CreateDirectory(outputDirectory);

        Vector3 originalPosition = camera.transform.position;
        RenderTexture originalTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        GameHUD hud = UnityEngine.Object.FindObjectOfType<GameHUD>();
        Canvas hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
        RenderMode originalCanvasMode = RenderMode.ScreenSpaceOverlay;
        Camera originalCanvasCamera = null;
        float originalPlaneDistance = 100f;

        if (hudCanvas != null)
        {
            originalCanvasMode = hudCanvas.renderMode;
            originalCanvasCamera = hudCanvas.worldCamera;
            originalPlaneDistance = hudCanvas.planeDistance;
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = camera;
            hudCanvas.planeDistance = 1f;
        }

        try
        {
            float[] previewCenters = { 0f, 12f, 32f, 64f, 96f };

            for (int index = 0; index < previewCenters.Length; index++)
            {
                camera.transform.position = new Vector3(
                    previewCenters[index],
                    0f,
                    -10f
                );

                RenderTexture target = new RenderTexture(1280, 720, 24);
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();

                Texture2D image = new Texture2D(
                    1280,
                    720,
                    TextureFormat.RGB24,
                    false
                );
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();

                File.WriteAllBytes(
                    Path.Combine(
                        outputDirectory,
                        "Level0_Section_" + (index + 1) + ".png"
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
                follow.enabled = followWasEnabled;
            }

            if (hudCanvas != null)
            {
                hudCanvas.renderMode = originalCanvasMode;
                hudCanvas.worldCamera = originalCanvasCamera;
                hudCanvas.planeDistance = originalPlaneDistance;
            }
        }
    }
}
