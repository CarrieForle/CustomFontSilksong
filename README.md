# CustomFont

Replace font with a `.ttf` or `.otf` font. Built for both players and developers.

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
dotnet docfx docfx.json --serve
```
