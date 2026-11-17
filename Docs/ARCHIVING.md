# Keep the project after uninstalling Unity and Meta Link

## What the repository preserves

This repository contains the Unity authoring source: `Assets` with its required `.meta` identifiers, pinned `Packages`, `ProjectSettings`, code, tests, documentation and third-party license notices. Restore it with Git, install the pinned Unity editor and allow Unity to download/import its packages. Opening the repository root in Unity Hub restores the project workflow.

The repository intentionally excludes executable builds, disposable import caches, participant reports and personal reference media. It is a source backup, not a backup of every file on the original computer.

## What to keep separately

- The complete known-working Windows build folder, including its executable, matching `_Data` directory, `UnityPlayer.dll` and other support files. The executable by itself is insufficient.
- Final presentation/document files and any reference photos or videos you want to retain.
- Any assessment data you explicitly need to retain, stored privately rather than in this public repository.

## Removing installed tools

Unity Editor, Unity Hub and Meta Horizon Link are separate applications. Use their registered uninstallers or Windows Installed Apps. Removing them does not require deleting the Unity project folder or the final presentation.

Unity's project `Library` is a regenerable import cache. Unity's global download/package cache can also be rebuilt, but rebuilding requires time and possibly new downloads. Remove caches only with Unity closed. Keep `Assets`, `Packages` and `ProjectSettings`.

The Windows build may still run in desktop mode without the Unity Editor. PC VR through Quest Link or Air Link requires the Meta runtime; reinstall Meta Horizon Link before using that mode again. A Windows build does not become a standalone Quest application when the PC tools are removed.
