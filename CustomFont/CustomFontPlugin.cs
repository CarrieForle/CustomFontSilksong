using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

enum ReplaceFontMode
{
    Disabled,
    EffectedByLanguage,
    All,
}