using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using static CustomFont.CustomFontPlugin;

namespace CustomFont;

[BepInDependency("org.silksong-modding.i18n")]
[BepInAutoPlugin(id: "io.github.carrieforle.customfont")]
partial class CustomFontPlugin : BaseUnityPlugin
{
    internal static ConfigEntry<ReplaceFontMode> configReplaceFontMode;
    internal static ConfigEntry<float> configFontScale;
    internal static string fontPath;
    internal static ManualLogSource logger;
    internal static TMProOld.TMP_FontAsset? fontAsset;
    internal static Dictionary<TMProOld.TextMeshPro, (float FontScale, TMProOld.TMP_FontAsset Font)> oldFonts = [];
    private Harmony harmony;

    private void Awake()
    {
        harmony = Harmony.CreateAndPatchAll(typeof(Patch), Id);
        logger = Logger;

        configReplaceFontMode = Config.Bind(
            "General",
            "ReplaceFontMode",
            ReplaceFontMode.EffectedByLanguage,
            Helper.Localized("OPTION_DESCRIPTION_REPLACE_FONT_MODE")
        );
        
        configFontScale = Config.Bind(
            "General",
            "FontScale",
            1f,
            Helper.Localized("OPTION_DESCRIPTION_FONT_SCALE", 1)
        );

        TryLoadFont([
            Path.Combine(Path.GetDirectoryName(Info.Location), "font.otf"),
            Path.Combine(Path.GetDirectoryName(Info.Location), "font.ttf"),
        ]);

        configReplaceFontMode.SettingChanged += (s, e) =>
        {
            PatchTMPros();
            var cfbls = FindObjectsByType<ChangeFontByLanguage>(FindObjectsSortMode.None);
            foreach (var cfbl in cfbls)
            {
                cfbl.SetFont();
            }
        };

        configFontScale.SettingChanged += (s, e) =>
        {
            if (configFontScale.Value <= 0)
            {
                logger.LogError("Invalid font scale. Resetted to 1.");
                configFontScale.Value = 1f;
            }
        };
    }

    private void Start()
    {
        harmony.PatchAll(typeof(LanguagePatch));
    }

    private static void TryLoadFont(IEnumerable<string> fontPaths)
    {
        try
        {
            fontPath = fontPaths
                .FirstOrDefault(fontPath => File.Exists(fontPath));

            if (fontPath is null)
            {
                logger.LogInfo("No font found.");
                return;
            }

            fontAsset = new FontAssetBuilder(new Font(fontPath))
            {
                AtlasHeight = 4096,
                AtlasWidth = 4096,
            }.Create();
        }
        catch (Exception ex)
        {
            logger.LogError($"Error while loading font: {ex.Message}\n{ex.StackTrace}");
        }
    }

    internal static void PatchTMPro(TMProOld.TextMeshPro tmpro, float scale)
    {
        fontAsset!.fallbackFontAssets = tmpro.font.fallbackFontAssets;
        if (fontAsset.fallbackFontAssets.IsNullOrEmpty())
        {
            fontAsset.fallbackFontAssets = [tmpro.font];
        }
        else if (fontAsset.fallbackFontAssets[0] != tmpro.font)
        {
            fontAsset.fallbackFontAssets.Insert(0, tmpro.font);
        }

        tmpro.font = fontAsset;
        tmpro.fontSize = scale;
    }

    internal static void UnpatchTMPro(TMProOld.TextMeshPro tmpro)
    {
        var fontAttr = oldFonts[tmpro];
        tmpro.font = fontAttr.Font;
        tmpro.fontSize = fontAttr.FontScale;
    }

    internal static void PatchTMPros()
    {
        if (fontAsset == null)
        {
            return;
        }

        var tmpros = GameCameras.SilentInstance.hudCamera.gameObject.GetComponentsInChildren<TMProOld.TextMeshPro>(true);
        foreach (var tmpro in tmpros)
        {
            oldFonts.TryAdd(tmpro, (tmpro.fontSize, tmpro.font));
        }

        foreach (var tmpro in tmpros)
        {
            if (configReplaceFontMode.Value == ReplaceFontMode.All)
            {
                PatchTMPro(tmpro, oldFonts[tmpro].FontScale * configFontScale.Value);
            }
            else
            {
                UnpatchTMPro(tmpro);
            }
        }

        logger.LogDebug($"Patched {tmpros.Length} TMPros in GameCameras");
    }
}

#pragma warning disable HARMONIZE002
#pragma warning disable HARMONIZE003
static class Patch
{
    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(ChangeFontByLanguage), nameof(ChangeFontByLanguage.SetFont), [])]
    private static void SetFont(ChangeFontByLanguage __instance)
    {
        if (fontAsset == null || configReplaceFontMode.Value == ReplaceFontMode.Disabled)
        {
            return;
        }

        PatchTMPro(__instance.tmpro, __instance.tmpro.fontSize * configFontScale.Value);
        Material fallbackMaterial = TMProOld.TMP_MaterialManager.GetFallbackMaterial(__instance.defaultMaterial, __instance.tmpro.fontSharedMaterial);
        __instance.FallbackMaterialReference = fallbackMaterial;
        __instance.tmpro.fontSharedMaterial = fallbackMaterial;
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ContinueGame))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame))]
    private static void PatchTMPros()
    {
		CustomFontPlugin.PatchTMPros();
    }
}

static class LanguagePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Language), nameof(Language.DoSwitch))]
    private static void ClearOldFonts()
    {
        oldFonts.Clear();
    }
}

#pragma warning restore HARMONIZE002
#pragma warning restore HARMONIZE003

static class Helper
{
    public static string path(GameObject go)
    {
        var t = go.transform;
        var sb = new StringBuilder(t.name);

        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, $"{t.name}/");
        }

        return sb.ToString();
    }

    public static LocalisedString Localized(string key)
    {
        return new LocalisedString($"Mods.{Id}", key);
    }

    public static string Localized(string key, params object[] args)
    {
        return string.Format(Language.Get(key, $"Mods.{Id}"), args);
    }
}

public class CustomFontException : Exception
{
    public CustomFontException(string message) : base(message)
    {

    }
}

enum ReplaceFontMode
{
    Disabled,
    EffectedByLanguage,
    All,
}

/// <summary>
/// A builder class to create font asset.
/// </summary>
public class FontAssetBuilder
{
    /// <summary>
    /// The source font to create font asset from.
    /// </summary>
    public Font Font { get; set; }

    /// <summary>
    /// The characters to be included in the font asset.
    /// If this is null, all characters are included.
    /// </summary>
    public ICollection<char>? CharList { get; set; }

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `0`
    /// </summary>
    public int FaceIndex { get; set; } = 0;

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `90`
    /// </summary>
    public int SamplingPointSize { get; set; } = 90;

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `9`
    /// </summary>
    public int AtlasPadding { get; set; } = 9;

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `GlyphRenderMode.SDFAA`
    /// </summary>
    public GlyphRenderMode RenderMode { get; set; } = GlyphRenderMode.SDFAA;

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `1024`
    /// </summary>
    public int AtlasWidth { get; set; } = 1024;

    /// <summary>
    /// A parameter directly passed to `FontAsset.CreateFontAsset()`.
    /// Default: `1024`
    /// </summary>
    public int AtlasHeight { get; set; } = 1024;

    /// <param name="font">The source font to create font asset from.</param>
    public FontAssetBuilder(Font font)
    {
        Font = font;
    }

    /// <summary>
    /// Create font asset with the properties.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="CustomFontException">Failed to create font asset</exception>
    public TMProOld.TMP_FontAsset Create()
    {
        TMProOld.TMP_FontAsset.FontAssetTypes fontAssetType;
        switch (RenderMode)
        {
            case GlyphRenderMode.SDF:
            case GlyphRenderMode.SDF8:
            case GlyphRenderMode.SDF16:
            case GlyphRenderMode.SDF32:
            case GlyphRenderMode.SDFAA:
            case GlyphRenderMode.SDFAA_HINTED:
                fontAssetType = TMProOld.TMP_FontAsset.FontAssetTypes.SDF;
                break;
            case GlyphRenderMode.SMOOTH_HINTED:
            case GlyphRenderMode.SMOOTH:
            case GlyphRenderMode.COLOR_HINTED:
            case GlyphRenderMode.COLOR:
            case GlyphRenderMode.RASTER_HINTED:
            case GlyphRenderMode.RASTER:
                fontAssetType = TMProOld.TMP_FontAsset.FontAssetTypes.Bitmap;
                break;
            default:
                throw new CustomFontException("GlyphRenderMode.DEFAULT is not allowed.");
        }

        FontAsset? newFontAsset = FontAsset.CreateFontAsset(Font, FaceIndex, SamplingPointSize, AtlasPadding, RenderMode, AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, false);

        if (newFontAsset == null)
        {
            throw new CustomFontException("Failed to create font asset.");
        }

        TMProOld.TMP_FontAsset tmp_FontAsset = ScriptableObject.CreateInstance<TMProOld.TMP_FontAsset>();
        tmp_FontAsset.name = Font.name;
        tmp_FontAsset.fallbackFontAssets = [];

        IEnumerable<uint>? charList = CharList
            ?.Select(ch => (uint)ch) ?? Enumerable
                .Range(0, 0x00ffff)
                .Select(c => (uint)c);

        if (!newFontAsset.TryAddCharacters(charList.ToArray(), out var missingChars, false))
        {
            var missingCharsStr = new string(missingChars.Select(c => (char)c).ToArray());

            if (CharList is not null)
            {
                logger.LogWarning($"Failed to add characters: \"{missingCharsStr}\". These will not be rendered.");
            }
        }

        TMProOld.TMP_Glyph[] glyphs = newFontAsset.characterTable
            .Select(character => NewToOld(character, AtlasHeight))
            .ToArray();

        logger.LogDebug($"Loaded {glyphs.Length} glyphs.");
        tmp_FontAsset.m_fontInfo = NewToOld(newFontAsset.faceInfo, AtlasWidth, AtlasHeight, AtlasPadding, glyphs.Length);
        tmp_FontAsset.AddGlyphInfo(glyphs);
        tmp_FontAsset.AddKerningInfo(NewToOld(newFontAsset));
        tmp_FontAsset.fontAssetType = fontAssetType;

        var texture2D = newFontAsset.atlasTexture;
        tmp_FontAsset.atlas = texture2D;
        logger.LogDebug($"Created {newFontAsset.atlasTextureCount} atlases.");

        TextureFormat textureFormat = ((RenderMode & (GlyphRenderMode)65536) == (GlyphRenderMode)65536) ? TextureFormat.RGBA32 : TextureFormat.Alpha8;
        int num;
        if ((RenderMode & (GlyphRenderMode)16) == (GlyphRenderMode)16)
        {
            num = 0;
            Material material;
            if (textureFormat == TextureFormat.Alpha8)
            {
                material = new Material(TextShaderUtilities.ShaderRef_MobileBitmap);
            }
            else
            {
                material = new Material(Shader.Find("TextMeshPro/Sprite"));
            }
            material.SetTexture(TextShaderUtilities.ID_MainTex, texture2D);
            material.SetFloat(TextShaderUtilities.ID_TextureWidth, (float)AtlasWidth);
            material.SetFloat(TextShaderUtilities.ID_TextureHeight, (float)AtlasHeight);
            tmp_FontAsset.material = material;
        }
        else
        {
            num = 1;
            Material material2 = new Material(TextShaderUtilities.ShaderRef_MobileSDF);
            material2.SetTexture(TextShaderUtilities.ID_MainTex, texture2D);
            material2.SetFloat(TextShaderUtilities.ID_TextureWidth, (float)AtlasWidth);
            material2.SetFloat(TextShaderUtilities.ID_TextureHeight, (float)AtlasHeight);
            material2.SetFloat(TextShaderUtilities.ID_GradientScale, (float)(AtlasPadding + num));
            material2.SetFloat(TextShaderUtilities.ID_WeightNormal, tmp_FontAsset.normalStyle);
            material2.SetFloat(TextShaderUtilities.ID_WeightBold, tmp_FontAsset.boldStyle);
            tmp_FontAsset.material = material2;
        }

        // From ChangeFontByLanguage.defaultMaterial
        tmp_FontAsset.material.renderQueue = 3000;
        tmp_FontAsset.material.globalIlluminationFlags =
            MaterialGlobalIlluminationFlags.None |
            MaterialGlobalIlluminationFlags.RealtimeEmissive |
            MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        /////

        tmp_FontAsset.ReadFontDefinition();
        FontEngine.UnloadFontFace();
        return tmp_FontAsset;
    }

    // https://docs.unity3d.com/Manual/UIE-font-asset-properties.html
    private static TMProOld.KerningTable NewToOld(FontAsset fontAsset)
    {
        Dictionary<uint, uint> glyphToUnicode = [];
        HashSet<uint> excludeGlyphs = [];

        // There might be multiple unicode code point for a glyph
        foreach (var ch in fontAsset.characterTable)
        {
            if (!glyphToUnicode.TryAdd(ch.glyphIndex, ch.unicode))
            {
                excludeGlyphs.Add(ch.glyphIndex);
            }
        }

        excludeGlyphs.Do(glyph => glyphToUnicode.Remove(glyph));
        var kernings = new TMProOld.KerningTable();

        foreach (var record in fontAsset.fontFeatureTable.glyphPairAdjustmentRecords)
        {
            if (glyphToUnicode.TryGetValue(record.firstAdjustmentRecord.glyphIndex, out var leftUnicode) &&
                glyphToUnicode.TryGetValue(record.secondAdjustmentRecord.glyphIndex, out var rightUnicode))
            {
                kernings.AddKerningPair(
                    (int)leftUnicode,
                    (int)rightUnicode,
                    record.firstAdjustmentRecord.glyphValueRecord.xAdvance
                );
            }
        }

        return kernings;
    }

    private static TMProOld.TMP_Glyph NewToOld(Character character, int atlasHeight)
    {
        return new TMProOld.TMP_Glyph
        {
            id = (int)character.m_Unicode,
            x = character.glyph.glyphRect.x,
            y = atlasHeight - character.glyph.glyphRect.y - character.glyph.metrics.height,
            width = character.glyph.metrics.width,
            height = character.glyph.metrics.height,
            xOffset = character.glyph.metrics.horizontalBearingX,
            yOffset = character.glyph.metrics.horizontalBearingY,
            xAdvance = character.glyph.metrics.horizontalAdvance,
            scale = 1f,
        };
    }

    private static TMProOld.FaceInfo NewToOld(FaceInfo newFaceInfo, float atlasWidth, float atlasHeight, float atlasPadding, int charCount)
    {
        return new TMProOld.FaceInfo
        {
            Name = newFaceInfo.familyName,
            PointSize = newFaceInfo.pointSize,
            Scale = newFaceInfo.scale,
            CharacterCount = charCount,
            LineHeight = newFaceInfo.lineHeight,
            Baseline = newFaceInfo.baseline,
            Ascender = newFaceInfo.ascentLine,
            CapHeight = newFaceInfo.capLine,
            Descender = newFaceInfo.descentLine,
            CenterLine = newFaceInfo.meanLine,
            SuperscriptOffset = newFaceInfo.superscriptOffset,
            SubscriptOffset = newFaceInfo.subscriptOffset,
            SubSize = newFaceInfo.subscriptSize,
            Underline = newFaceInfo.underlineOffset,
            UnderlineThickness = newFaceInfo.underlineThickness,
            TabWidth = newFaceInfo.tabWidth,
            Padding = atlasPadding,
            AtlasWidth = atlasWidth,
            AtlasHeight = atlasHeight,
        };
    }
}