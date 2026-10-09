# Backdrop first Windows release plan

## Goal

The Backdrop repository is already public. Build an installer-first Windows 11 x64 release candidate that installs and uninstalls without Developer Mode. It must accept one to nine images, create one PNG, and send a multi-file Explorer selection through one command. Microsoft Store publication follows the installer release and needs a Store account.

## Architecture and constraints

Keep the .NET WinForms application, System.Drawing renderer, JSON settings, and native C++ `IExplorerCommand`. Reuse the current self-check and shell harness. Do not add a service, account, cloud API, or new application framework. Build x64 first. Keep source files unchanged and preserve old settings. Work only in `projects/backdrop` for product files; this plan is the sole workspace document.

The current app already has ratios, row/grid layout, ordered drag-and-drop input, dark controls, a bounded live preview, background modes, output actions, image limits, collision-safe export, and a native selection-aware shell command. These do not need a redesign. The current shell installer is a developer prototype: `install-shell.ps1` registers a loose manifest and requires `AppModelUnlock`; `AppxManifest.xml` uses a prototype publisher. `build-shell.ps1` creates an unsigned sparse MSIX. These files do not form a consumer installer.

Microsoft requires package identity for a command in the primary Windows 11 context menu. A sparse package must be signed by a certificate trusted on the target machine; its manifest publisher must match the signing identity. A trusted production identity is an external release requirement. The existing `IExplorerCommand` can serve a classic-menu route while the installer and signing path are finished, but that route must be checked with real multi-selection before inclusion. Do not claim a primary-menu command from a classic registry verb. [Explorer command guide](https://learn.microsoft.com/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer), [external-location packaging guide](https://learn.microsoft.com/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [signing guide](https://learn.microsoft.com/windows/msix/package/sign-msix-package-guide).

## Technology and release path

- Installer first, then Microsoft Store. Retarget to .NET 10 LTS now. Use the isolated SDK at `artifacts/tools/dotnet/dotnet.exe` (10.0.401), existing self-contained x64 publish, MSVC x64, Windows SDK 10.0.26100, and verified portable Inno Setup 6.7.3 at `artifacts/tools/InnoSetupPortable/tools/ISCC.exe`. Keep tools and artifacts ignored.
- Install binaries to a stable per-user application directory. Install and uninstall the Explorer command as part of the same lifecycle. Do not point shell registration at a build folder or downloaded archive. Do not change Developer Mode or install a development certificate on a customer's computer.
- For the first candidate, register the existing C++ `IExplorerCommand` in the classic **Show more options** menu through 64-bit HKCU COM and per-extension `ExplorerCommandHandler` verbs with `MultiSelectModel=Player`. Include no prototype MSIX in the consumer payload. A future primary Windows 11 menu requires a signed sparse package with a matching trusted publisher identity. An unsigned installer is allowed as a candidate; state SmartScreen and publisher status accurately.

## Tasks

### 1. Establish a release baseline

**Files:** `Backdrop.csproj`, `Program.cs`, `BackdropRenderer.cs`, `SettingsStore.cs`, `MainForm.cs`, `BackgroundSettingsDialog.cs`, `BackdropControls.cs`, `shell/*`, `scripts/*`.

- [ ] Retarget `Backdrop.csproj` to `net10.0-windows`; keep old JSON defaults and CLI arguments.
- [ ] Build and run `--self-check` with `.\artifacts\tools\dotnet\dotnet.exe`. Publish self-contained win-x64; build the C++ DLL and run the native harness with two sample images. Record exact command output and versions.
- [ ] Keep the harness result separate from proof that Explorer shows the menu.

Example commands: `.\artifacts\tools\dotnet\dotnet.exe build Backdrop.csproj -c Release`; `.\artifacts\tools\dotnet\dotnet.exe run --project Backdrop.csproj -- --self-check artifacts/self-check`; `.\artifacts\tools\dotnet\dotnet.exe publish Backdrop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/release/payload`.

**Acceptance:** Build, self-check, and native harness pass; generated sources retain their hashes; one selected group creates one output. The baseline record gives exact commands, versions, and failures. Never use the self-check as proof that Explorer displays the command.

### 2. Build a repeatable consumer installer

**Files:** new `installer/Backdrop.iss`, new `scripts/classic-shell.ps1`, minimal release-build script if needed, `MainForm.cs`, `README.md`. Reuse `shell/BackdropShell.cpp` and the native harness. Keep development package scripts out of the consumer payload.

- [ ] Implement `classic-shell.ps1 -Action Status|Register|Unregister`. Status returns `installed` or `not-installed`. Before each write or deletion, verify the known CLSID, DLL path, verb handler, and owned values. Refuse foreign registration; delete only matching Backdrop values.
- [ ] Detect the current-user `Backdrop.ShellPrototype` package and remove it only if its registered command CLSID matches Backdrop's known CLSID. Do not remove a package by name alone.
- [ ] Build a stable per-user Inno installer with Start menu and uninstall entries. Unregister owned COM before DLL replacement or removal, then register after successful install. Set `CloseApplications=yes`; do not force process termination. Handle locked DLL and reboot outcomes without stale registration. Keep images and `%LOCALAPPDATA%\Backdrop\settings.json` on uninstall.
- [ ] Adapt `MainForm.cs` to the classic shell script contract and label its menu location as **Show more options**. Build with `.\artifacts\tools\InnoSetupPortable\tools\ISCC.exe installer\Backdrop.iss`. Inspect the payload for prototype MSIX, keys, and developer registration scripts.

The existing DLL locates `Backdrop.exe` beside itself and needs no package identity for classic COM activation. The classic route is temporary. Review registry scope, COM activation, prototype migration, upgrades, and uninstall cleanup before any public artifact.

**Manual release gate:** On a clean standard Windows 11 x64 account with Developer Mode off, install, launch, upgrade, and uninstall work. Test real Explorer selections of 1, 2, 9, and 10 images, plus mixed/unsupported selections. Two and nine valid images each make one PNG; 10 or unsupported files do not launch. No installed registry path points at temporary build output. Uninstall removes only owned menu and app files, preserving settings and images. The native harness does not prove menu visibility.

### 3. Close release-critical product and accessibility gaps

**Files:** `MainForm.cs`, `Program.cs`, `BackdropRenderer.cs`, `BackgroundSettingsDialog.cs`, `SettingsStore.cs` only where a reproduced issue requires it.

- [ ] Add **Remove selected** and **Clear** actions to the image list. Update selection, preview, and Create state; keep keyboard access.
- [ ] Add Cancel during PNG creation. Extend `Generate(paths, settings, CancellationToken = default)`, pass the token through existing render checks, and check after render and encode, before `FileMode.CreateNew`, during write, and after write. On cancellation or failure, delete only the newly created output. Prevent a second create action until the first ends.
- [ ] Add focused self-check coverage for selection correction where practical, cancellation and partial-output cleanup, unchanged sources, and existing output names. Manually check keyboard focus, 800 × 600 and 960 × 720 at normal and enlarged scaling, corrupt/oversize inputs, unwritable output, rapid preview edits, and output actions.

**Acceptance:** All controls are usable by keyboard and remain visible at the minimum supported window size; Remove/Clear update the list and preview; a canceled render leaves no output file; errors identify the failing file or output location; rapid edits show only the latest preview; a failed export leaves no partial PNG; successful output actions open the created file and its folder.

### 4. Release evidence and public repository

**Files:** `README.md`, new `docs/release-checklist.md`, optional GitHub workflow under `.github/workflows/` if CI can run the existing build and self-check without secrets. No broad documentation rewrite.

- [ ] Replace prototype and Developer Mode instructions with exact install, update, uninstall, x64 and Windows 11 requirements, supported formats and limits, and the classic-menu location. State local data behavior and unsigned-candidate SmartScreen status. Use Simplified Technical English.
- [ ] Record build, self-check, harness, clean-install, real Explorer, accessibility, error-path, checksum, and uninstall results. Capture representative app and output images. Publish a versioned installer and checksum only after every clean-machine and real Explorer gate passes.

**Acceptance:** A reader can install and remove Backdrop without an SDK, compiler, Developer Mode, or PowerShell commands; the download is the same build that passed the release checks; no credentials, private certs, generated temp files, or unrelated Passage files are committed. Before a Backdrop commit or push, confirm `git rev-parse --show-toplevel` is `C:/work/job helper/projects/backdrop` and `git remote -v` points to `daviddll396/Backdrop`.

### 5. Microsoft Store follow-up

After the installer release, choose the Store identity and submission route. A sparse identity package is not itself a direct Store submission. Reuse the app and installer for the supported MSI/EXE Store route if that meets Store policy, or build a full MSIX package if a Store-managed install is desired. The Store route needs the publisher account and its package identity. Validate the installed Store build and Explorer command separately. Do not put Store account setup on the critical path for the first installer candidate.

## Review gates and risks

An independent architecture and security review must inspect COM ownership checks, prototype-package migration, installer upgrade and uninstall order, locked DLL handling, and cancellation cleanup. This candidate is not a public release until clean Windows 11 installation and real Explorer selection tests pass. A trusted publisher identity blocks the primary-menu release; a Store account blocks Store publication. Neither blocks work on the classic-menu installer candidate.
