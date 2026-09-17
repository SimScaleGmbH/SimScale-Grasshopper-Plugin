# Yak package

This folder holds the [Yak](https://developer.rhino3d.com/guides/yak/what-is-yak/) package
manifest for this plugin — Yak is Rhino's built-in package manager (`_PackageManager` in
Rhino, or the `yak` CLI). Publishing here lets users install/update the plugin from inside
Rhino instead of the current manual zip + installer.exe process.

`manifest.yml` is a template only — it doesn't contain the plugin binaries. The build
script (`scripts/Build-YakPackage.ps1`) copies it, together with the built `.gha` and its
dependency `.dll`s from `latest_stable/SimScale/src`, into a scratch folder and runs
`yak build` there.

## One-time setup: a McNeel account

**McNeel** (Robert McNeel & Associates) is the company that makes Rhino and Grasshopper —
they also run Yak's package server. Publishing a package needs a free McNeel/Rhino account
(the same login used for a Rhino license works). No separate signup, no cost.

## What "hosting a feed" means (and why you don't need to)

A Yak "source" (or feed) is just a location Rhino's Package Manager checks for `.yak`
files. By default every Rhino install points at McNeel's own public server,
`yak.rhino3d.com` — free, McNeel-hosted, no setup required on our side. Running `yak push`
uploads the package there and it becomes installable by anyone via `_PackageManager` in
Rhino.

"Hosting a feed" only comes up if you *don't* want that — e.g. an internal-only package not
listed publicly. That means pointing Rhino at a private folder/server instead of the
default one, which is extra infrastructure we don't need. Since this plugin is already a
public GitHub repo, publishing to the public server is the right call — skip private
hosting entirely.

## Building locally

1. Get the `yak` CLI — either from your own Rhino install
   (`C:\Program Files\Rhino 8\System\Yak.exe`) or the standalone tool:
   <https://files.mcneel.com/yak/tools/latest/yak.exe>.
2. From the repo root:
   ```powershell
   ./scripts/Build-YakPackage.ps1 -YakExe "C:\Program Files\Rhino 8\System\Yak.exe"
   ```
   This produces a `simscale-grasshopper-plugin-<version>-rh...-win.yak` file at the repo
   root.
3. Sanity-check it by dragging the `.yak` file onto a running Rhino window, or
   `yak install <file>`.

## Publishing (manual)

```powershell
yak login          # opens a browser to sign in with your McNeel account
yak push simscale-grasshopper-plugin-*.yak
```

`yak login` stores a token locally (`%appdata%\McNeel\yak.yml`) — you only need to do this
once per machine.

## Publishing from CI

`.github/workflows/build-yak-package.yml` builds the package on every push and, on a
version tag (`v*`), pushes it automatically — but only if the `YAK_TOKEN` repository
secret is set. To enable that:

1. Run `yak login --ci` on your own machine — this prints a non-expiring API key instead
   of storing a browser session.
2. Add that value as a repository secret named `YAK_TOKEN`
   (Settings → Secrets and variables → Actions).

Until that secret exists, the workflow still builds the `.yak` and uploads it as a
downloadable artifact — it just won't publish automatically.

## A heads-up on size

The current build output (`latest_stable/SimScale/src`) is ~150 MB, almost entirely VTK's
dependency DLLs (used for mesh interpolation of results). That's large for a Yak package —
worth trimming unused VTK modules at some point, but not something this scaffold
addresses.
