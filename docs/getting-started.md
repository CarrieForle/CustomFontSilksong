# Introduction

This mod allows you to create `TMProOld.Font_Asset` from `.ttf` and `.otf` fonts, which are used for dialogues and popups.

This mod does not produce a <xref:UnityEngine.Font>. Which means you cannot use this mod to swap fonts on `UnityEngine.UI.Text`.

## Include package

Include the package from [Nuget](https://www.nuget.org/packages/CustomFontSilksong) as follow:

```sh
dotnet add package CustomFontSilksong
```

Add this attribute on top of your plugin class:

```cs
[BepInDependency("io.github.carrieforle.customfont")]
```

If you use Thunderstore, make sure to put this as part of the dependencies in the manifest.

## Getting Started

To create a `TMProOld.TMP_FontAsset`, instantiate <xref:UnityEngine.Font> and use <xref:CustomFont.FontAssetBuilder>.

```cs
Font font = new Font("your/path/to/font.ttf");
TMProOld.TMP_FontAsset fontAsset = new FontAssetBuilder(font).Create();
```

<xref:CustomFont.FontAssetBuilder> has multiple properties that can be set to configure font asset creation during <xref:CustomFont.FontAssetBuilder.Create>. Most properties are directly passed to the underlying `FontAsset.CreateFontAsset()`.