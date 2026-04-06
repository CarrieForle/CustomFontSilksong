# Getting Started

To create a `TMP_FontAsset`, instantiate `Font` and use `FontAssetBuilder`.

```cs
Font font = new Font("your/path/to/font.ttf");
TMProOld.TMP_FontAsset fontAsset = new FontAssetBuilder(font).Create();
```

`FontAssetBuilder` has multiple properties that can be set to configure font asset creation during `Create()`. Most properties are directly passed to the underlying `FontAsset.CreateFontAsset()`.