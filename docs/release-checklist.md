# Backdrop 0.1.0 release checklist

## Target

Prepare a per-user installer for Windows 11 on native x64 hardware. Microsoft Store distribution is a separate step. The installer candidate is unsigned and can show a SmartScreen warning.

## Automated checks

- [x] Build with .NET SDK 10.0.401 without warnings or errors.
- [x] Run the exact self-contained staged app's self-check.
- [x] Check composition modes, input limits, output names, source preservation, and background settings.
- [x] Check removal and clearing of selected images.
- [x] Check that a cancelled export creates no final PNG or temporary file.
- [x] Check foreign registry values are preserved during shell unregistration.
- [x] Run the native shell command harness against the staged payload.
- [x] Compile with Inno Setup 6.7.3 and record the installer SHA-256.

The registry safety fixture checks unregistration ownership. It does not prove registration rollback. The native harness checks COM activation and selected-file processing. It does not prove that Explorer shows the menu.

## Installer checks on the development machine

- [x] Install for the current user without administrator access.
- [x] Remove only the known Backdrop developer sparse package.
- [x] Activate the registered shell command with two and nine pictures; create one output per selection.
- [x] Upgrade an existing consumer installation.
- [x] Uninstall and confirm that owned shell entries and app files are removed.
- [x] Uninstall with a missing app DLL and confirm that owned shell entries are removed.
- [x] Reinstall and open the installed app.
- [x] Confirm that source image hashes are unchanged after installer checks.
- [ ] Confirm that existing saved preferences are unchanged.

The settings file was absent before the installer checks and remained absent. Preservation of an existing saved settings file still needs a check.

## Required before a public binary release

- [ ] Install on a clean Windows 11 x64 machine or standard user account with no SDK or Developer Mode.
- [ ] Use Explorer's **Show more options** menu with one, two, and nine pictures.
- [ ] Confirm that a multi-selection creates one composition.
- [ ] Check ten pictures, mixed supported and unsupported files, unreadable files, and image size limits.
- [ ] Check paths with spaces and Unicode characters.
- [ ] Check the installed app at normal and increased display scale.
- [ ] Check keyboard navigation, image-list Delete, and Create/Cancel labels.
- [ ] Check uninstall cancellation and upgrade while the app is open.
- [ ] Decide whether to sign the installer before publication.

## Microsoft Store release

- [ ] Set the final publisher identity and package name.
- [ ] Prepare a Store package and its Explorer integration.
- [ ] Add Store screenshots and accurate product text.
- [ ] Run the Store certification checks.
- [ ] Submit the package after the installer release gates pass.
