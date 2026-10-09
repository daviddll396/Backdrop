# Backdrop

Backdrop combines up to nine pictures into one PNG. The window shows a live preview. Add files with **Add images** or drop them into the window. Use **Move up** and **Move down**, or press **Alt+Up** and **Alt+Down**, to set their order. Pictures keep their original shape and are never cropped.

## Composition

- **Canvas ratio:** Auto, Wide 16:9, Square 1:1, or Portrait 4:5.
- **Layout:** Auto, Row, or Grid. Auto places two to four portrait pictures in one row. Other groups use a grid. Grid centers its final row when it is not full.
- **Auto ratio:** A single picture uses its own ratio. A row uses the natural ratio of its pictures, limited to 0.5:1 through 3:1. A grid uses the average picture ratio and its row and column count.
- **Long edge:** The output's longest side, from 640 to 4096 pixels. The 16:9 default at 1920 pixels makes a 1920 × 1080 PNG.
- **Padding:** A percentage applied to each canvas edge.
- **Shadow:** A soft Gaussian shadow under each picture.
- **Background:** Choose **Automatic gradient**, **Solid color**, **Two-color gradient**, or **Pattern**. Automatic gradient samples the selected pictures and is the default for older preference files. Pattern uses one base color with soft grain or spaced dots.

Preview renders use a 1000-pixel long edge. The final PNG uses the selected long edge. The preview updates after a short pause when settings, pictures, or order change. **Save preferences** stores settings in `%LOCALAPPDATA%\Backdrop\settings.json`; changes are not saved automatically.

Choose **Background** above the preview to edit the background. Color fields use opaque six-digit hex colors. Background changes update the preview. **Apply** keeps them in the current session; choose **Save preferences** in the main window to keep them for later.

Backdrop preserves the source files and never replaces an output. A single picture is named `<image>-backdrop.png`. A group is named `backdrop-composition.png`. If that name exists, Backdrop adds a number.

## Run

Use the .NET 9 SDK on Windows:

```powershell
dotnet run --project Backdrop.csproj
```

Publish a self-contained x64 application:

```powershell
dotnet publish Backdrop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/v2
.\scripts\build-shell.ps1
```

The project has no application package references. Self-contained publishing restores the .NET runtime pack.

To make a PNG from a command prompt or a shortcut, run:

```powershell
Backdrop.exe --generate "C:\Photos\one.jpg" "C:\Photos\two.jpg"
```

The command prints the output path when it has a console. When Explorer starts it, a small window appears for up to eight seconds. It does not take focus. Choose **Open image** or **Show folder** to open the result.

The **Enable Explorer menu** button registers the packaged Explorer command for the current user. The menu command is **Create composition with Backdrop** and sends the whole selection to one composition. Registration needs loose package registration enabled in Windows. The script checks this setting and does not change it. Use **Remove Explorer menu** to unregister it.

The current package is a development prototype. It is not signed for Store distribution. A release package needs a publisher identity that matches its signing certificate and must pass the Store or trusted-certificate signing process. The built-in self-check does not invoke Explorer or confirm that Windows displays the menu; test registration and the menu on the target Windows 11 machine.

## Inputs and checks

Supported formats are PNG, JPEG, BMP, GIF, and TIFF. Each picture can be at most 40 megapixels. A selection can be at most 80 megapixels total.

Run the built-in checks and create wide and compact form captures:

```powershell
dotnet run --project Backdrop.csproj -- --self-check artifacts/self-check
```

The check covers canvas ratios, image order, row and grid placement, background modes and patterns, legacy and invalid preferences, output names, source preservation, cancellation, and 960 × 720 and 800 × 600 form captures. It does not invoke Explorer or confirm that the context menu appears.
