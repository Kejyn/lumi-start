# Changelog

## [Unreleased] - Targeted Options adjustments, 2026-10-16

- Replaced the Graphics category tabs with stacked, titled sections in the existing scroll area, keeping the footer fixed.
- Use the localized Options label for the Pause menu button.
- Suppress mouse UI hover while gamepad is active, restoring pointer interaction on keyboard/mouse activity.

## [Unreleased] - UI consistency, 2026-10-16

- Grouped the thirteen existing Graphics controls into Display, Image and Details pages, using native TMP dropdowns with explicit selection and capability-filtered values.
- Unified Language and Graphics captions as localized name/value labels; opening or navigating a list preserves the draft, with Apply/Back/Reset and storage contracts retained.
- Aligned the Audio slider presentation and selector margins with the existing Interface controls, preserving the shared Options footer.
- Routed binding feedback through the common Options status area while preserving capture and error diagnostics.
- Added a Starter-owned cursor service after Input: gamepad activity hides the cursor, keyboard/mouse activity shows it, scene changes retain the policy and shutdown restores the initial visibility without changing cursor locking.

## [Unreleased] - UI corrections, 2026-10-16

- Aligned the No/Yes actions horizontally in the confirmation for returning to the menu and both dialogs for deleting saves, retaining their behavior and styling.
- Replaced the cycling language control with a native two-step TMP dropdown: opening or navigating the list preserves the draft; selecting previews, Apply saves and Back closes the list first.
- Hide the entire menu Canvas before controlled Bootstrap shutdown, including Save/Load controls outside registered screens, to prevent a flash during exit.

## [Unreleased] - Localization

- Added official Unity Localization 1.5.13, English/French locales and one complete GameUI String Table Collection for fixed labels, dynamic information, confirmations, saves and input prompts.
- Reused the existing Interface language selector and Settings LanguageCode with immediate preview, Apply persistence and discard/reset restoration; retained all existing preference keys and gameplay save contracts.
- Added asynchronous locale initialization, coalesced previews and awaited shutdown alongside the existing Bootstrap; Core remains independent of Unity Localization.
- Preserved the approved interface and localized dynamic formatting, save dates, control labels and credits; added focused completeness, preference, navigation and lifecycle checks.
- Documented translation and future locale/font integration; explicitly configured Addressables content construction with future Player builds, without running a Player build in this pass.

## [Unreleased] - Graphics settings

- Extended the existing Options draft with capability-filtered rendering controls and Low/Medium/High presets with derived Custom state.
- Added a project-owned URP adapter using runtime pipeline clones and restoring native quality, camera and display state on shutdown.
- Preserved Settings V1 keys, gameplay saves, the approved menu layout and the Windows Auto API list containing DX12 and DX11.
- Added targeted preference, rendering, input/navigation and lifecycle regressions; documented platform and performance limits.

## [Unreleased] - Repository maintenance

- Updated installation, architecture and contributor guides for the two independent repositories.
- Excluded local agent state, credentials, signing material, player data and validation artifacts.
- Kept technical notices and compatibility contracts while removing internal session reports.

## [Unreleased] - Final Starter Cleanup, 2026-10-15

- Consolidated the active composition, gameplay, UI, save DTO and audio under Assets/Game with neutral type and assembly names; retained asset GUIDs and the approved interface.
- Removed unused example scenes, asset generators and Unity template resources.
- Retained tests for current Starter responsibilities and Core services rather than preserving historical suite counts.
- Updated installation, architecture, new-game, build/test and manual-review guides. Generated build reports now belong under ignored Builds/Reports.
- Preserved save identities/schema, Settings keys, user data and required URP/TMP resources.

## [Unreleased] - Release Candidate Polish, 2026-10-14

- Added dedicated ProductMenu/ProductGameplay scenes, an original native uGUI/TMP canvas and cloned project input, preserving legacy scenes/assets and regressions.
- Added per-session Boot, New/Continue, real three-slot selection with metadata and confirmed deletion, rolling credits and awaited quit confirmation.
- Added Gameplay/Controls/Graphics/Audio/Interface tabs, real reusable Core Input rebinding and separate versioned Starter presentation/override preferences.
- Kept the existing save model, game identity and Settings key; New Game never overwrites an occupied slot or autosaves.
- Made product scenes the build entry points while retaining all five legacy scenes.

## [Unreleased] - Phase 5, 2026-10-14

- Added a reproducible Editor-only Windows x64 Mono/IL2CPP builder with Main Menu first and restored temporary settings.
- Added an isolated Development Player variant and validation scripts outside Assets for real native input, settings, audio, saves and process restart observations.
- Archived an independent public-API consumer and reproducible validation evidence.
- Added new-game, build/test and physical-device checklist documentation.
- Preserved existing runtime APIs, assets, dependencies and regression tests. Final v0.1.0 tags await explicit user validation.

## Phase 4 - 2026-10-13

- Composed independent Persistence service with a dedicated demo identity, separate from Settings/PlayerPrefs.
- Extended the existing Gameplay/pause Canvas with three Save/Load slots, editable counter, deletion confirmation and diagnostic feedback.
- Added Starter-owned versioned cube/counter DTO and native Input System bindings for keyboard, pointer and gamepad.
- Added isolated real-disk integration and repeated Play-session tests; retained all preceding regressions.
- Preserved UI selection across asynchronous operations and used a project-owned pointer action while the Gameplay UI map is inactive.

## Phase 3 - 2026-10-13

- Extended the existing Menu/Gameplay scenes and Canvas with an Options draft accessible from Main and Pause.
- Composed independent Settings and Audio services, synchronizing saved volumes before music playback.
- Added explicit Apply/save, cancel on Back and draft-only Reset Defaults, with namespaced PlayerPrefs separate from gameplay saves.
- Added native Master/Music/SFX mixer and locally generated temporary music/UI/2D/3D sound assets with documented provenance.
- Added project-owned gameplay SFX bindings and Settings/Audio integration regression coverage.
- Preserved the four earlier Core runtime modules and both earlier demonstrations; added no external package dependency.
- Treat canceled Bootstrap readiness as normal teardown in both Starter scene controllers, and format volume labels with the percent character available in the demonstration font.

## Phase 2 - 2026-10-13

- Added the interactive Menu/Gameplay demonstration, project-owned Input Actions, uGUI/TMP Canvas prefab and an Editor asset builder.
- Added pause/resume, cancelable return confirmation, scene transitions with Input suspension and an optional time freeze.
- Added integration coverage through virtual keyboard/mouse/gamepad device events and repeated sessions without Domain Reload.
- Deferred Pause/Cancel context changes until Update to preserve Input System 1.19's native UI callback chain.
- Imported TMP essential resources needed by the demonstration; retained the existing Unity package versions.
- Kept the Phase 1 demonstration, Bootstrap/Scene Flow implementation and template SampleScene.

## Phase 0 and Phase 1 - 2026-10-13

- Established the sibling local UPM dependency and project integration tests.
- Added the A → B → A demonstration and independent local Git repositories.
