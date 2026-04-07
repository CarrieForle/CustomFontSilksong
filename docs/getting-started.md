# Include package

Include the package from [Nuget](https://www.nuget.org/packages/CustomFontSilksong) as follow:

```sh
dotnet add package CustomFontSilksong --version 0.1.0
```

# Getting Started

To create a `TMP_FontAsset`, instantiate <xref:UnityEngine.Font> and use <xref:CustomFont.FontAssetBuilder>.

```cs
Font font = new Font("your/path/to/font.ttf");
TMProOld.TMP_FontAsset fontAsset = new FontAssetBuilder(font).Create();
```

<xref:CustomFont.FontAssetBuilder> has multiple properties that can be set to configure font asset creation during <xref:CustomFont.FontAssetBuilder.Create>. Most properties are directly passed to the underlying `FontAsset.CreateFontAsset()`.