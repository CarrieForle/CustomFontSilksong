/*
Skip cutscene prompt in the title is UnityEngine.UI.Text
*/

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
using static CustomFont.CustomFontPlugin;

namespace CustomFont;

[BepInDependency("org.silksong-modding.i18n")]
[BepInAutoPlugin(id: "io.github.carrieforle.customfont")]
partial class CustomFontPlugin : BaseUnityPlugin, IDisposable
{
    internal static ConfigEntry<ReplaceFontMode> configReplaceFontMode;
    internal static ConfigEntry<float> configFontScale;
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

        TryLoadFont();
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
        };
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

        try
        {
            currentFontPath = fontPaths
                .FirstOrDefault(fontPath => File.Exists(fontPath));

            if (currentFontPath is null)
            {
                logger.LogInfo("No font found.");
                return;
            }

            logger.LogInfo($"Use font at \"{Path.GetFileName(currentFontPath)}\".");

            fontAsset = new FontAssetBuilder(new Font(currentFontPath))
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