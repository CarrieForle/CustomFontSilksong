/*
Skip cutscene prompt in the title is UnityEngine.UI.Text
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;
using static CustomFont.CustomFontPlugin;

namespace CustomFont;

[BepInDependency("org.silksong-modding.i18n")]
[BepInAutoPlugin(id: "io.github.carrieforle.customfont")]
partial class CustomFontPlugin : BaseUnityPlugin, IDisposable
{
    internal static ConfigEntry<ReplaceFontMode> configReplaceFontMode;
    internal static ConfigEntry<float> configFontScale;
    private static ConfigEntry<bool> configAllCharsAtlas;
    internal static ManualLogSource logger;
    internal static TMProOld.TMP_FontAsset? fontAsset;
    internal static Dictionary<TMProOld.TextMeshPro, (float FontScale, TMProOld.TMP_FontAsset Font)> oldFonts = [];
    private static string[] fontPaths = [];
    private static string? currentFontPath;
    private FileSystemWatcher fontWatcher;
    private Harmony harmony;

    private void Awake()
    {
        harmony = Harmony.CreateAndPatchAll(typeof(Patch), Id);
        logger = Logger;

        string assemblyPath = Path.GetDirectoryName(Info.Location);
        fontPaths = [
            Path.Combine(assemblyPath, "font.otf"),
            Path.Combine(assemblyPath, "font.ttf"),
        ];

        fontWatcher = new(assemblyPath)
		{
			NotifyFilter = NotifyFilters.CreationTime |
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size
		};

		fontWatcher.Changed += (s, e) => OnFileChanged(e.FullPath, e.FullPath);
        fontWatcher.Created += (s, e) => OnFileChanged(null, e.FullPath);
        fontWatcher.Deleted += (s, e) => OnFileChanged(e.FullPath, null);
        fontWatcher.Renamed += (s, e) => OnFileChanged(e.OldFullPath, e.FullPath);
        fontWatcher.EnableRaisingEvents = true;
    }

    private void Start()
    {
        configReplaceFontMode = Config.Bind(
            "General",
            "ReplaceFontMode",
            ReplaceFontMode.EffectedByLanguage,
            Localized("OPTION_DESCRIPTION_REPLACE_FONT_MODE")
        );

        configFontScale = Config.Bind(
            "General",
            "FontScale",
            1f,
            Localized("OPTION_DESCRIPTION_FONT_SCALE", 1)
        );

        configAllCharsAtlas = Config.Bind(
            "General",
            "AllCharsAtlas",
            false,
            Localized("OPTION_DESCRIPTION_ALL_CHARS_ATLAS")
        );

        configReplaceFontMode.SettingChanged += (s, e) =>
        {
            TryUnpatchTMPros();

            if (configReplaceFontMode.Value == ReplaceFontMode.All)
            {
                TryPatchTMPros();
            }

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
                logger.LogWarning("Invalid font scale. Resetted to 1.");
                configFontScale.Value = 1f;
            }

            if (configReplaceFontMode.Value != ReplaceFontMode.Disabled)
            {
                var cfbls = FindObjectsByType<ChangeFontByLanguage>(FindObjectsSortMode.None);
                foreach (var cfbl in cfbls)
                {
                    cfbl.SetFont();
                }

                if (configReplaceFontMode.Value == ReplaceFontMode.All)
                {
                    foreach (var (tmpro, attr) in oldFonts)
                    {
                        tmpro.fontSize = attr.FontScale * configFontScale.Value;
                    }
                }
            }
        };

        configAllCharsAtlas.SettingChanged += (s, e) =>
        {
            currentFontPath = null;
        };

        TryLoadFont();
    }

    private void OnFileChanged(string? oldFilePath, string? newFilePath)
    {
        if (currentFontPath is not null &&
            ((oldFilePath is not null && fontPaths.Contains(oldFilePath)) ||
            (newFilePath is not null && fontPaths.Contains(newFilePath))))
        {
            logger.LogInfo("Detected font file changed. Prepare to reload font.");
            currentFontPath = null;
        }
    }

    public void Dispose()
    {
        fontWatcher.Dispose();
    }

    internal static void TryLoadFont()
    {
        if (currentFontPath is not null)
        {
            return;
        }

        currentFontPath = fontPaths
            .FirstOrDefault(fontPath => File.Exists(fontPath));

        if (currentFontPath is null)
        {
            logger.LogInfo("No font found.");
            return;
        }

        logger.LogInfo($"Loading font at \"{Path.GetFileName(currentFontPath)}\".");

        long fileSize = new FileInfo(currentFontPath).Length;
        if (fileSize > 1e6)
        {
            logger.LogWarning($"Font file is large (> {fileSize * 1e-6:F1}MB)! It may take several minutes to to create font atlas.");
        }

        var fab = new FontAssetBuilder(new Font(currentFontPath))
        {
            AtlasHeight = 8192,
            AtlasWidth = 8192,
            SamplingPointSize = 70,
            AtlasPadding = 7,
        };

        if (!configAllCharsAtlas.Value)
        {
            fab.AddChars(32, 126)
                .AddChars(128, 254);

            foreach (string text in Language.Settings.sheetTitles)
            {
                string languageFileContents = Language.GetLanguageFileContents(text);
                if (!string.IsNullOrEmpty(languageFileContents))
                {
                    using XmlReader xmlReader = XmlReader.Create(new StringReader(languageFileContents));
                    while (xmlReader.ReadToFollowing("entry"))
                    {
                        xmlReader.MoveToFirstAttribute();
                        string value = xmlReader.Value;
                        xmlReader.MoveToElement();
                        string text2 = xmlReader.ReadElementContentAsString().Trim();
                        text2 = text2.UnescapeXml();
                        fab.AddChars(text2);
                    }
                }
            }
        }

        fontAsset = fab.Create();
    }

    internal static void PatchTMPro(TMProOld.TextMeshPro tmpro, float scale, Material sourceMaterial)
    {
        fontAsset!.fallbackFontAssets = tmpro.font.fallbackFontAssets;
        if (fontAsset.fallbackFontAssets.IsNullOrEmpty())
        {
            fontAsset.fallbackFontAssets = [tmpro.font];
        }
        else if (!fontAsset.fallbackFontAssets.Contains(tmpro.font))
        {
            fontAsset.fallbackFontAssets.Insert(0, tmpro.font);
        }

        tmpro.fontSize = scale;
        tmpro.font = fontAsset;

        Debug.Assert(fontAsset.material == tmpro.fontSharedMaterial);

        Material oldMaterial = TMProOld.TMP_MaterialManager.GetFallbackMaterial(sourceMaterial, tmpro.fontSharedMaterial);
        tmpro.fontSharedMaterial = oldMaterial;
    }

    internal static void TryPatchTMPros()
    {
        if (fontAsset == null || configReplaceFontMode.Value != ReplaceFontMode.All)
        {
            return;
        }

        logger.LogInfo($"Patching {oldFonts.Count} TMPro");
        oldFonts = oldFonts
            .Where(t => t.Key != null)
            .ToDictionary(t => t.Key, t => t.Value);

        foreach (var (tmpro, attr) in oldFonts)
        {
            PatchTMPro(tmpro, attr.FontScale * configFontScale.Value, tmpro.fontSharedMaterial);
        }
    }

    internal static void UnpatchTMPro(TMProOld.TextMeshPro tmpro)
    {
        var fontAttr = oldFonts[tmpro];
        tmpro.font = fontAttr.Font;
        tmpro.fontSize = fontAttr.FontScale;
    }

    internal static void TryUnpatchTMPros()
    {
        if (fontAsset == null)
        {
            return;
        }

        logger.LogInfo($"Unpatching {oldFonts.Count} TMPro");
        oldFonts = oldFonts
            .Where(t => t.Key != null)
            .ToDictionary(t => t.Key, t => t.Value);

        foreach (var (tmpro, _) in oldFonts)
        {
            UnpatchTMPro(tmpro);
        }
    }

    public static string PathOf(GameObject go)
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

        PatchTMPro(__instance.tmpro, __instance.tmpro.fontSize * configFontScale.Value, __instance.defaultMaterial);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ContinueGame))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame))]
    private static void PatchTMPros()
    {
        TryLoadFont();
        TryPatchTMPros();
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(TMProOld.TextMeshPro), nameof(TMProOld.TextMeshPro.Awake))]
    private static void RecordAndPatchTMPro(TMProOld.TextMeshPro __instance)
    {
        oldFonts.TryAdd(__instance, (__instance.fontSize, __instance.font));

        if (fontAsset == null || configReplaceFontMode.Value != ReplaceFontMode.All)
        {
            return;
        }

        PatchTMPro(__instance, __instance.fontSize * configFontScale.Value, __instance.fontSharedMaterial);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(ChangeFontByLanguage), nameof(ChangeFontByLanguage.Awake))]
    private static void ExcludeCfbl(ChangeFontByLanguage __instance)
    {
        oldFonts.Remove(__instance.tmpro);
    }
}

#pragma warning restore HARMONIZE002
#pragma warning restore HARMONIZE003

enum ReplaceFontMode
{
    Disabled,
    EffectedByLanguage,
    All,
}