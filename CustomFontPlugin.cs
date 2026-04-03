using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using static CustomFont.CustomFontPlugin;

namespace CustomFont;

[BepInAutoPlugin(id: "io.github.carrieforle.customfont")]
public partial class CustomFontPlugin : BaseUnityPlugin
{
    private static ConfigEntry<bool> configOverrideMenu;
    private static ConfigEntry<bool> configOverrideGameplay;
    private static ConfigEntry<bool> configOverrideHud;
    private static string[] fontPaths;
    public static ManualLogSource logger;
    private static Font? font;
    private static TMProOld.TMP_FontAsset? fontAsset;
    private static bool tryLoadedFont = false;

    private void Awake()
    {
        Harmony.CreateAndPatchAll(typeof(CustomFontPlugin), Id);
        logger = Logger;
        configOverrideMenu = Config.Bind(
            "Override",
            "Menu",
            false,
            "Apply font to start and pause menu."
        );

        configOverrideGameplay = Config.Bind(
            "Override",
            "Gameplay",
            true,
            "Apply font to dialogues, popups, etc."
        );

        configOverrideHud = Config.Bind(
            "Override",
            "Hud",
            false,
            "Apply font to HUD and inventory."
        );

        fontPaths = [
            Path.Combine(Path.GetDirectoryName(Info.Location), "font.otf"),
            Path.Combine(Path.GetDirectoryName(Info.Location), "font.ttf"),
        ];

        TryLoadFont();
    }

#pragma warning disable HARMONIZE003
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ChangeFontByLanguage), nameof(ChangeFontByLanguage.SetFont), [])]
    static void PatchFont(ChangeFontByLanguage __instance)
    {
        if (fontAsset != null)
        {
            __instance.tmpro.font = fontAsset;
        }
    }
#pragma warning restore HARMONIZE003

    private static void TryLoadFont()
    {
        try
        {
            if (!tryLoadedFont)
            {
                string? fontPath = fontPaths
                    .FirstOrDefault(fontPath => File.Exists(fontPath));

                if (fontPath is null)
                {
                    logger.LogInfo("No font found.");
                    tryLoadedFont = true;
                    return;
                }

                fontAsset = new FontAssetBuilder(fontPath).Build();
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error while loading font.\n{ex.StackTrace}");
        }
        finally
        {
            tryLoadedFont = true;
        }
    }
}

public class CustomFontException : Exception
{
    public CustomFontException(string message) : base(message)
    {

    }
}

//
public class FontAssetBuilder
{
    private Font? font;
    public Font? Font
    {
        get => font;
        set
        {
            if (value != null)
            {
                font = value;
                fontPath = null;
            }
        }
    }

    private string? fontPath;
    public string? FontPath
    {
        get => fontPath;
        set
        {
            if (value != null)
            {
                fontPath = value;
                font = null;
            }
        }
    }

    public ICollection<char>? CharList { get; set; }
    public int FaceIndex { get; set; } = 0;
    public int SamplingPointSize { get; set; } = 90;
    public int AtlasPadding { get; set; } = 9;
    public GlyphRenderMode RenderMode { get; set; } = GlyphRenderMode.SDFAA;
    public int AtlasWidth { get; set; } = 1024;
    public int AtlasHeight { get; set; } = 1024;

    public FontAssetBuilder(Font font)
    {
        this.font = font;
    }

    public FontAssetBuilder(string fontPath)
    {
        this.fontPath = fontPath;
    }

    public TMProOld.TMP_FontAsset Build()
    {
        logger.LogInfo($"Loaded font: \"{fontPath}\"");
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

        FontAsset? newFontAsset;

        if (FontPath is not null)
        {
            logger.LogInfo("Using font path");
            newFontAsset = FontAsset.CreateFontAsset(FontPath, FaceIndex, SamplingPointSize, AtlasPadding, RenderMode, AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, false);
        }
        else
        {
            logger.LogInfo("Using font class");
            newFontAsset = FontAsset.CreateFontAsset(Font, FaceIndex, SamplingPointSize, AtlasPadding, RenderMode, AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, false);
        }

        logger.LogInfo("Created new font asset");

        if (newFontAsset == null)
        {
            throw new CustomFontException("Failed to create font asset.");
        }

        TMProOld.TMP_FontAsset tmp_FontAsset = ScriptableObject.CreateInstance<TMProOld.TMP_FontAsset>();

        IEnumerable<uint>? charList = CharList?.Select(ch => (uint)ch);
        if (charList is null)
        {
            charList = Enumerable
                .Range(0, 0x00ffff)
                .Select(c => (uint)c);
        }

        if (!newFontAsset.TryAddCharacters(charList.ToArray(), out var missingChars, false))
        {
            var missingCharsStr = new string(missingChars.Select(c => (char)c).ToArray());

            if (CharList is not null)
            {
                logger.LogWarning($"Failed to add characters: \"{missingCharsStr}\". These will not be rendered.");
            }
        }

        tmp_FontAsset.name = newFontAsset.name;
        TMProOld.TMP_Glyph[] glyphs = newFontAsset.characterTable
            .Select(character => NewToOld(character, AtlasHeight))
            .ToArray();

        logger.LogDebug($"Loaded {glyphs.Length} glyphs.");
        tmp_FontAsset.m_fontInfo = NewToOld(newFontAsset.faceInfo, AtlasWidth, AtlasHeight, AtlasPadding, glyphs.Length);
        tmp_FontAsset.AddGlyphInfo(glyphs);
        tmp_FontAsset.AddKerningInfo(new TMProOld.KerningTable());
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
        return tmp_FontAsset;
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

    private static TMProOld.FaceInfo NewToOld(UnityEngine.TextCore.FaceInfo newFaceInfo, float atlasWidth, float atlasHeight, float atlasPadding, int charCount)
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