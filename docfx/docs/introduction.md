---
title: Introduction
---

## Introduction

Silksong uses TextMeshPro to render various texts in places such as dialogues, HUD, and cutscenes. The game uses a rather old version of TextMeshPro which does not have a way to instantiate `TMProOld.TMP_FontAsset` at runtime.

This mod allows you to instantiate `TMProOld.TMP_FontAsset` from OpenType (`.otf`) or TrueType (`.ttf`) fonts from a file to replace fonts.

> [!NOTE]
> Do not confuse `TMP_FontAsset` with `FontAsset`. The former is what Silksong uses (namespace `TMProOld`) and it's what you're supposed to replace. The latter is the built-in, newer version in Unity (namespace `UnityEngine.TextCore.Text`).

## Include Package

Include the package from [Nuget](https://www.nuget.org/packages/CustomFontSilksong) as follow:

```sh
dotnet add package CustomFontSilksong
```

Add this attribute on top of your plugin class:

```cs
[BepInDependency("io.github.carrieforle.customfont")]
```

If you want to upload your mod to Thunderstore, see [here](https://thunderstore.io/c/hollow-knight-silksong/p/CarrieForle/CustomFont/) for the dependency string.