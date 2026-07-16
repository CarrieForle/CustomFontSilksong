---
title: Getting Started
---

## Getting Started

To create a `TMProOld.TMP_FontAsset`, instantiate <xref:UnityEngine.Font> and use <xref:CustomFont.FontAssetBuilder>.

```cs
try
{
    Font font = new Font("your/path/to/font.ttf");
    TMProOld.TMP_FontAsset fontAsset = new FontAssetBuilder(font).Create();
}
catch (CustomFontException ex)
{
    logger.LogError("Failed to create font.");
}
```

<xref:CustomFont.FontAssetBuilder> has multiple properties that can be set to configure font asset creation during <xref:CustomFont.FontAssetBuilder.Create>. Most properties are directly passed to the underlying `FontAsset.CreateFontAsset()`.

## Character List

By default, all characters in the font are included in the font asset. This will cause a poor performance if you have a large font file (> 1MB) upon calling <xref:CustomFont.FontAssetBuilder.Create>. Fonts like CJK are especially large, containing thousands of glyphs, and it can take several minutes just for the method to finish.

One way to improve the performance is specifying which characters to be included by assigning <xref:CustomFont.FontAssetBuilder.CharList>. You can also use <xref:CustomFont.FontAssetBuilder.AddChars(System.Collections.Generic.ISet{System.UInt32})> and its overloaded methods to limit the characters to be included. These two are not mutually exclusive. You can use both at the same time.