---
title: Getting Started
---

## Getting Started

To create a `TMProOld.TMP_FontAsset`, instantiate <xref:UnityEngine.Font>, instantiate <xref:CustomFont.FontAssetBuilder> with the said font and call <xref:CustomFont.FontAssetBuilder.Create>.

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

You can set properties to configure font asset creation. The following code creates a font asset with 8192x8192 atlas.

```cs
Font font = new Font("font.ttf");
FontAssetBuilder fab = new FontAssetBuilder(font);

fab.AtlasHeight = 8192;
fab.AtlasWidth = 8192;

TMProOld.TMP_FontAsset fontAsset = fab.Create();
```

Check the [API](xref:CustomFont.FontAssetBuilder) for all the properties you can set.

## Character List

By default, all characters in the font are included in the font asset. This will cause a poor performance if you have a large font file (> 1MB) upon calling <xref:CustomFont.FontAssetBuilder.Create>. Fonts like CJK are especially large, containing thousands of glyphs, and it can take several minutes just for the method to finish.

One way to improve the performance is specifying which characters to be included. You can do so by assigning <xref:CustomFont.FontAssetBuilder.CharList> with a set of unicode scalar values. The following code creates a font asset that can only render capital A to Z.

```cs
HashSet<uint> charList = new HashSet<uint>();

// Insert all capital alphabets
for (uint i = 65; i <= 90; i++)
{
    charList.Add(i);
}

Font font = new Font("font.ttf");
FontAssetBuilder fab = new FontAssetBuilder(font)
fab.CharList = charList;
TMProOld.TMP_FontAsset fontAsset = fab.Create();
```

More often than not, you might have every character you need stored in a string. In this case you should use <xref:CustomFont.FontAssetBuilder.AddChars(System.String)>. The following code is equivalent to the one above.

```cs
string s = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

Font font = new Font("font.ttf");
TMProOld.TMP_FontAsset fontAsset = new FontAssetBuilder(font)
    .AddChar(s)
    .Create();
```

It's worth to know that <xref:CustomFont.FontAssetBuilder.AddChars(System.String)> (and its overloaded methods) just add unicode scalar values to <xref:CustomFont.FontAssetBuilder.CharList> under the hood. Therefore you can combine both approaches or call <xref:CustomFont.FontAssetBuilder.AddChars(System.String)> multiple times.

> [!NOTE]
> Do not iterate `char` from a `string` and add them to <xref:CustomFont.FontAssetBuilder.CharList> because a single unicode scalar value might be comprised of 2 `char`s! Instead, use <xref:CustomFont.FontAssetBuilder.AddChars(System.String)>.