/*
Notes:
- Skip cutscene prompt in the title is UnityEngine.UI.Text
- Can't patch in Awake() because some gameobjects instantiate from others and cause the loss of track of original gameobject
- There is a check in setters of tmpro.fontSize or tmpro.font to check if value has changed, so no penalty for assignment
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
using UnityEngine.SceneManagement;
using static CustomFont.CustomFontPlugin;

namespace CustomFont;

using CachedTMPros = Dictionary<TMProOld.TextMeshPro, (float FontSize, TMProOld.TMP_FontAsset Font)>;

[BepInDependency("org.silksong-modding.i18n")]
[BepInAutoPlugin(id: "io.github.carrieforle.customfont")]
partial class CustomFontPlugin : BaseUnityPlugin, IDisposable
{
    internal static ConfigEntry<ReplaceFontMode> configReplaceFontMode;
    internal static ConfigEntry<float> configFontScale;
    private static ConfigEntry<bool> configAllCharsAtlas;
    internal static ManualLogSource logger;
    internal static TMProOld.TMP_FontAsset? fontAsset;
    internal static CachedTMPros oldTMPros = [];

    // Some cfbls can't be patched immediately e.g., in Quest menu, that will otherwise cause font size to be applied twice. The reason is unknown but I'm too fed up to find out why.
    internal static bool cfblCanPatch = true;
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
            TryUpdateTMPros();
        };

        configFontScale.SettingChanged += (s, e) =>
        {
            if (configFontScale.Value <= 0)
            {
                logger.LogWarning("Invalid font scale. Resetted to 1.");
                configFontScale.Value = 1f;
            }

            TryUpdateTMPros();
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
        if (fileSize > 1e6 && configAllCharsAtlas.Value)
        {
            logger.LogWarning($"Font file is large (> {fileSize * 1e-6:F1}MB)! It may take several minutes to to create font atlas.");
        }

        var fab = new FontAssetBuilder(new Font(currentFontPath))
        {
            AtlasHeight = 8192,
            AtlasWidth = 8192,
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
        if (tmpro == null)
        {
            oldTMPros.Remove(tmpro);
            return;
        }

        tmpro.fontSize = scale;

        if (tmpro.font == fontAsset)
        {
            return;
        }

        fontAsset!.fallbackFontAssets = tmpro.font.fallbackFontAssets;
        if (fontAsset.fallbackFontAssets.IsNullOrEmpty())
        {
            fontAsset.fallbackFontAssets = [tmpro.font];
        }
        else if (!fontAsset.fallbackFontAssets.Contains(tmpro.font))
        {
            fontAsset.fallbackFontAssets.Insert(0, tmpro.font);
        }

        tmpro.font = fontAsset;

        Debug.Assert(fontAsset.material == tmpro.fontSharedMaterial);

        Material oldMaterial = TMProOld.TMP_MaterialManager.GetFallbackMaterial(sourceMaterial, tmpro.fontSharedMaterial);
        tmpro.fontSharedMaterial = oldMaterial;
    }

    private static void UnpatchTMPro(TMProOld.TextMeshPro tmpro, (float FontSize, TMProOld.TMP_FontAsset Font) attr)
    {
        if (tmpro == null)
        {
            oldTMPros.Remove(tmpro);
            return;
        }
        var a = tmpro == null ? "null" : tmpro.GetInstanceID().ToString();
        var b = tmpro.font == null ? "null" : tmpro.font.name;
        var c = attr.Font == null ? "null" : attr.Font.name;

        if (a == "null" || b == "null" || c == "null")
        {
            logger.LogInfo($"({a} ,{b}, {c})");
        }

        tmpro.font = attr.Font;
        tmpro.fontSize = attr.FontSize;
    }

    internal static void TryUnpatchTMPro(TMProOld.TextMeshPro tmpro)
    {
        if (oldTMPros.TryGetValue(tmpro, out var attr))
        {
            UnpatchTMPro(tmpro, attr);
        }
    }

    internal static void TryUpdateTMPros()
    {
        if (fontAsset == null)
        {
            return;
        }

        if (configReplaceFontMode.Value != ReplaceFontMode.All)
        {
            logger.LogDebug($"Unpatching TMPro");

            foreach (var (tmpro, attr) in oldTMPros)
            {
                UnpatchTMPro(tmpro, attr);
            }
        }
        else
        {
            logger.LogDebug($"Patching TMPro");

            foreach (var (tmpro, attr) in oldTMPros)
            {
                PatchTMPro(tmpro, attr.FontSize * configFontScale.Value, tmpro.fontSharedMaterial);
            }
        }

        var cfbls = FindObjectsByType<ChangeFontByLanguage>(FindObjectsSortMode.None);

        foreach (var cfbl in cfbls)
        {
            cfbl.SetFont();
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
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.LevelActivated))]
    private static void AddTMProsInScene()
    {
        logger.LogInfo("Entering Scene");
        cfblCanPatch = true;
        if (fontAsset == null)
        {
            return;
        }

        var scene = SceneManager.GetActiveScene();
        var tmpros = scene.GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<TMProOld.TextMeshPro>(true));

        var tmprosAdded = false;
        foreach (var tmpro in tmpros)
        {
            if (tmpro.fontSharedMaterial == null || tmpro.font == null)
            {
                continue;
            }

            var parent = tmpro.transform.parent.gameObject;

            // ignore letter in button prompt
            if (parent != null && parent.GetComponent<MenuButtonIcon>() != null)
            {
                continue;
            }

            tmprosAdded = tmprosAdded || oldTMPros.TryAdd(tmpro, (tmpro.fontSize, tmpro.font));
        }

        if (tmprosAdded)
        {
            logger.LogDebug("Found scene tmpros");
            TryUpdateTMPros();
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(OpeningSequence), nameof(OpeningSequence.Start))]
    // Act cards (ACT I Pharloom, etc) are not cfbl, despite them differ between languages.
    private static void AddTMProsInActCard(OpeningSequence __instance)
    {
        if (fontAsset == null || configReplaceFontMode.Value == ReplaceFontMode.Disabled)
        {
            return;
        }

        logger.LogDebug("Patching act cards");
        var tmpros = __instance.GetComponentsInChildren<TMProOld.TextMeshPro>(true);
        foreach (var tmpro in tmpros)
        {
            // We just patch them immediately since they are destroyed immediately after fadeout.
            PatchTMPro(tmpro, tmpro.fontSize * configFontScale.Value, tmpro.fontSharedMaterial);
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(CinematicPlayer), nameof(CinematicPlayer.FinishInGameVideo))]
    // Act II are not cfbl, despite them differ between languages.
    private static void AddTMProsInActCard(CinematicPlayer __instance)
    {
        if (fontAsset == null || __instance.actCard == null || configReplaceFontMode.Value == ReplaceFontMode.Disabled)
        {
            return;
        }

        logger.LogDebug("Patching act cards");
        var tmpros = __instance.actCard.GetComponentsInChildren<TMProOld.TextMeshPro>(true);
        foreach (var tmpro in tmpros)
        {
            // We just patch them immediately since they are destroyed immediately after fadeout.
            PatchTMPro(tmpro, tmpro.fontScale * configFontScale.Value, tmpro.fontSharedMaterial);
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(SimpleShopMenu), nameof(SimpleShopMenu.Awake))]
    // Price tags are only instantiated when Hornet interact with the shop.
    private static void AddTMProsInShop(SimpleShopMenu __instance)
    {
        if (fontAsset == null)
        {
            return;
        }

        __instance.pane.OnPaneStart += () =>
        {
            var tmpros = __instance.GetComponentsInChildren<TMProOld.TextMeshPro>(true);

            bool tmprosAdded = false;
            foreach (var tmpro in tmpros)
            {
                if (tmpro.fontSharedMaterial == null || tmpro.font == null)
                {
                    continue;
                }

                tmprosAdded = tmprosAdded || oldTMPros.TryAdd(tmpro, (tmpro.fontSize, tmpro.font));
            }

            if (tmprosAdded)
            {
                logger.LogDebug("Found shop price tags");
                TryUpdateTMPros();
            }
        };
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(ChangeFontByLanguage), nameof(ChangeFontByLanguage.SetFont), [])]
    private static void SetFont(ChangeFontByLanguage __instance)
    {
        var tmpro = __instance.tmpro;
        if (tmpro == null || fontAsset == null || !cfblCanPatch)
        {
            // The original SetFont will restore the font and font size.
            // We can do nothing.
            return;
        }

        oldTMPros.TryAdd(tmpro, (tmpro.fontSize, tmpro.font));
        if (configReplaceFontMode.Value != ReplaceFontMode.Disabled)
        {
            PatchTMPro(tmpro, oldTMPros[tmpro].FontSize * configFontScale.Value, __instance.defaultMaterial);
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(QuestItemManager), nameof(QuestItemManager.ResetExtraDisplay))]
    [HarmonyPatch(typeof(QuestItemManager), nameof(QuestItemManager.SetDisplay), [typeof(InventoryItemSelectable)])]
    private static void PatchFontSize(QuestItemManager __instance)
    {
        if (fontAsset == null)
        {
            // This is called every time the player opens quest, where fontSize is always reset.
            return;
        }

        if (oldTMPros.TryGetValue(__instance.descriptionText, out var attr))
        {
            if (configReplaceFontMode.Value == ReplaceFontMode.Disabled)
            {
                __instance.descriptionText.fontSize = attr.FontSize;
            }
            else
            {
                __instance.descriptionText.fontSize = attr.FontSize * configFontScale.Value;
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.Start))]
    private static void ClearTMPros()
    {
        logger.LogDebug("Clearing TMPros");
        cfblCanPatch = false;
        oldTMPros.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.ContinueGame))]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.StartNewGame))]
    private static void StartGame()
    {
        GameCameras.SilentInstance.hudCamera.gameObject
            .GetComponentsInChildren<TMProOld.TextMeshPro>(true)
            .Do(tmpro => oldTMPros.TryAdd(tmpro, (tmpro.fontSize, tmpro.font)));

        TryLoadFont();
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(TMProOld.TextMeshPro), "OnDestroy")]
    private static void RemoveTMPro(TMProOld.TextMeshPro __instance)
    {
        oldTMPros.Remove(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(InventoryItemListManager<InventoryItemCollectable, CollectableItem>), nameof(InventoryItemListManager<,>.UpdateList))]
    private static void UnpatchInventory(InventoryItemListManager<InventoryItemCollectable, CollectableItem> __instance)
    {
        var tmpros = __instance.GetComponentsInChildren<TMProOld.TextMeshPro>(true);

        foreach (var tmpro in tmpros)
        {
            TryUnpatchTMPro(tmpro);
        }
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.HigherThanNormal)]
    [HarmonyPatch(typeof(InventoryItemListManager<InventoryItemCollectable, CollectableItem>), nameof(InventoryItemListManager<,>.UpdateList))]
    private static void PatchInventory(InventoryItemListManager<InventoryItemCollectable, CollectableItem> __instance)
    {
        var tmpros = __instance.GetComponentsInChildren<TMProOld.TextMeshPro>(true);

        foreach (var tmpro in tmpros)
        {
            oldTMPros.TryAdd(tmpro, (tmpro.fontSize, tmpro.font));
        }

        TryUpdateTMPros();
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