# CustomFont

Replace font with a OpenType or TrueType font. Built for both players and developers.

## Limitation

Not all fonts are replacable. Specifically those in the game menu and pause menu are not replacable, but everything else should do.

## Player Usage

Install the mod (either manual or via a mod manager) and place a font file named `font.otf` or `font.ttf` in the same folder as the DLL. Your folder structure should look like this:

```
.
└── BepinEx/
    └── plugins/
        └── CarrieForle-CustomFont/
            ├── icon.png
            ├── font.otf <- your font
            ├── manifest.json
            ├── README.md
            ├── CustomFont.dll
            └── CustomFont.pdb
```

If you use a mod manager (e.g., r2modman), the above structure should be in a profile folder. If you installed BepinEx manually, it should be in the Silksong installation folder.

### Options

Go to Options > Mods > Custom Font

#### All Chars Atlas

The created font atlas includes characters that are not seen in the game. If this is off then it only includes characters that are seen in the game. The change will take effect when you load a save file.

> [!WARNING]
> Enabling this option will cause noticeable load delay if the font file is large (> 1MB), which can take from seconds up to several minutes.

#### Font Scale

The scale of font size e.g., 1.5 means 150% bigger.

#### Replace Font Mode

Where to apply custom font:
- Disabled: don't apply custom font
- Effected by language: only apply custom font to texts that are effected by languages e.g., dialogues. Texts such as number of items, button prompt are not applied
- All: apply custom font for all possible texts

> [!NOTE]
> Even when you use "All", texts in the menues and some cutscenes remain unchanged as they use different UI toolkits which this mod does not cover.

## Developer Usage

See [documentation](https://carrieforle.github.io/CustomFontSilksong/).

## Build

.NET 10 is required.

Create `SilksongPath.props` under `CustomFont`. Copy and paste the following text and edit as needed.

```xml
<Project>
  <PropertyGroup>
    <SilksongFolder>SilksongInstallPath</SilksongFolder>
    <!-- If you use a mod manager rather than manually installing BepInEx, this should be a profile directory for that mod manager. -->
    <SilksongPluginsFolder>$(SilksongFolder)/BepInEx/plugins</SilksongPluginsFolder>
  </PropertyGroup>
</Project>
```

```sh
dotnet build -c Release
```

### Build documentation

```sh
dotnet tool restore
dotnet docfx docfx/docfx.json --serve
```
