---
title: Introduction
---

## Introduction

This mod allows you to create `TMProOld.TMP_Font_Asset` from OpenType (`otf`) or TrueType (`.ttf`) fonts from a file, which are used for texts in places such as inventory, dialogues, HUD.

This mod does not produce a <xref:UnityEngine.Font>, which means you cannot use this mod to swap fonts on `UnityEngine.UI.Text`.

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