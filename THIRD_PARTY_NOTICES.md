# Credits and third-party notices

## Upstream project

The working project originated from [Oasis Uniandes / Fire-Extinguisher-Training](https://github.com/Oasis-Uniandes/Fire-Extinguisher-Training). Its **MIT License**, copyright **2024 Oasis Uniandes**, remains in the root `LICENSE` file.

The upstream README credited supervision by Julián Camilo Mora Valbuena, Abel Arismendy, Juan Esteban Rodríguez Ospino and Juan José Sierra Alarcón; and research/development by Juan José Sierra Alarcón, Andrés Felipe Aristizábal Miranda, Santiago Rodriguez, Shadith Perez and Nicolás Rueda. These credits describe the upstream project, not authorship of every department-specific addition.

## Unity XR samples

The retained controller models, materials, input-action asset and controller animator are a minimal referenced subset of Unity's XR Interaction Toolkit Starter Assets, version 3.5.0. Original `.meta` identifiers and source notices are retained. The sample assembly's unused `Unity.XR.OpenXR` reference is removed because that package is absent and the active application uses Oculus XR.

The toolkit is copyright © 2026 Unity Technologies and licensed under the **Unity Companion License for Unity-dependent projects**. See [the included license notice](ThirdParty/Unity-XR-Interaction-Toolkit-LICENSE.md) and [Unity's license terms](https://unity.com/legal/licenses/unity-companion-license). These assets are not relicensed under the root MIT license.

## Registry packages

Unity, URP, TextMesh Pro, Oculus XR, XR Hands, Meta SDK and other dependencies are restored from the pinned package manifests. Their respective licenses apply. Their package caches, SDK binary distributions and unrelated sample packs are not copied into this repository.

## Audio and reference material

The repository includes the project's generated narration/equipment audio and synthesized alarm. The public snapshot replaces the unrelated imported fire-loop asset with `Assets/FireSafety/Audio/FireCrackle.wav`, a deterministic noise-based procedural loop generated specifically for this source snapshot. The working project's original imported audio and unused Asset Store packs are not redistributed here.

Personal department photographs, WhatsApp recordings, video, proposal PDFs, participant data and local editor logs are excluded. The use of the IIT Kharagpur name identifies the reconstruction's reference setting; no institutional endorsement is claimed.
