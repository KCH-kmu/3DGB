using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public sealed class MidtermSpriteImportPostprocessor : AssetPostprocessor
{
    private const string AssetRoot = "Assets/MidtermSprites/";
    private const string RawSpriteRoot = AssetRoot + "Raw/";
    private const string ManifestPath = AssetRoot + "sprite_manifest.csv";

    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(RawSpriteRoot, StringComparison.Ordinal)
            || !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TextureImporter importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.npotScale = TextureImporterNPOTScale.None;

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        Dictionary<string, GUID> existingIds = new Dictionary<string, GUID>(
            StringComparer.Ordinal);
        foreach (SpriteRect existing in dataProvider.GetSpriteRects())
        {
            existingIds[existing.name] = existing.spriteID;
        }

        importer.GetSourceTextureWidthAndHeight(out int textureWidth, out int textureHeight);
        SpriteRect[] sprites = LoadSpritesForAtlas(
            assetPath,
            textureWidth,
            textureHeight,
            existingIds);
        if (sprites.Length == 0)
        {
            Debug.LogWarning($"No midterm sprite slices found for {assetPath}.");
            return;
        }

        dataProvider.SetSpriteRects(sprites);
        dataProvider.Apply();
    }

    private static SpriteRect[] LoadSpritesForAtlas(
        string atlasAssetPath,
        int textureWidth,
        int textureHeight,
        IReadOnlyDictionary<string, GUID> existingIds)
    {
        if (!File.Exists(ManifestPath))
        {
            Debug.LogError($"Midterm sprite manifest is missing: {ManifestPath}");
            return Array.Empty<SpriteRect>();
        }

        string atlasSource = atlasAssetPath.Substring(AssetRoot.Length);
        string[] lines = File.ReadAllLines(ManifestPath);
        if (lines.Length < 2)
        {
            return Array.Empty<SpriteRect>();
        }

        string[] headers = lines[0].TrimStart('\ufeff').Split(',');
        Dictionary<string, int> columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < headers.Length; index++)
        {
            columns[headers[index]] = index;
        }

        string[] required =
        {
            "sprite_name", "source", "left", "top", "right", "bottom"
        };
        foreach (string field in required)
        {
            if (!columns.ContainsKey(field))
            {
                Debug.LogError($"Midterm sprite manifest is missing column '{field}'.");
                return Array.Empty<SpriteRect>();
            }
        }

        List<SpriteRect> sprites = new List<SpriteRect>();
        for (int lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] values = line.Split(',');
            if (values.Length < headers.Length
                || !string.Equals(
                    values[columns["source"]],
                    atlasSource,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int left = ParseInt(values[columns["left"]]);
            int top = ParseInt(values[columns["top"]]);
            int right = ParseInt(values[columns["right"]]);
            int bottom = ParseInt(values[columns["bottom"]]);

            left = Mathf.Clamp(left, 0, textureWidth);
            right = Mathf.Clamp(right, left, textureWidth);
            top = Mathf.Clamp(top, 0, textureHeight);
            bottom = Mathf.Clamp(bottom, top, textureHeight);
            if (right <= left || bottom <= top)
            {
                continue;
            }

            string spriteName = values[columns["sprite_name"]];
            SpriteRect sprite = new SpriteRect
            {
                name = spriteName,
                rect = new Rect(
                    left,
                    textureHeight - bottom,
                    right - left,
                    bottom - top),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = existingIds.TryGetValue(spriteName, out GUID existingId)
                    ? existingId
                    : GUID.Generate()
            };
            sprites.Add(sprite);
        }

        return sprites.ToArray();
    }

    private static int ParseInt(string value)
    {
        return int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
