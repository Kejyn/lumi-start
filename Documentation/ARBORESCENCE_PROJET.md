# Arborescence et responsabilités du projet

Le Starter actif se trouve sous `Assets/Game`, pour **Unity 6000.3.13f1**.
Les arbres omettent les `.meta` et le détail des ressources TMP/URP ; ces fichiers
restent nécessaires aux références Unity et doivent être conservés.

## Espace de travail

~~~text
workspace/
├── lumi-start/                          Projet Unity du Starter, dépôt Git indépendant
└── lumi-core/                           Package UPM réutilisable, dépôt Git indépendant
~~~

Le manifeste `Lumi-start/Packages/manifest.json` référence
`"com.lumivale.lumi-core": "file:../../lumi-core"`, relatif au dossier `Packages`.
Sur un système sensible à la casse, conserver le nom voisin `lumi-core`.
Le code du package n'est pas dupliqué dans le Starter.

## Jeu actif : Assets/Game

~~~text
Assets/Game/
├── Scenes/
│   ├── GameMenu.unity                    Entrée : Start, menu, options, slots et crédits
│   └── GameDemo.unity                    Gameplay, pause et sauvegardes
├── Runtime/
│   ├── Lumivale.Start.Game.asmdef        Assembly runtime du jeu
│   ├── Bootstrap/
│   │   ├── GameBootstrap.cs             Composition des services et hôte persistant
│   │   ├── GameSettingsAudioBinding.cs   Préférences appliquées à Audio et préchargement
│   │   └── GameSessionState.cs           État de session et snapshot à transmettre
│   ├── Configuration/
│   │   ├── GamePreferences.cs            Présentation et overrides Gameplay
│   │   ├── GameGraphicsBackend.cs        Application native sur clones URP et restauration
│   │   ├── GameGraphicsPresets.cs        Politique Low/Medium/High et calcul de Custom
│   │   ├── GameLocalizationService.cs    Initialisation et aperçu de la locale officielle
│   │   └── GameScenePaths.cs             Routes des deux scènes actives
│   ├── Gameplay/
│   │   ├── GameSceneController.cs        Navigation, pause, Input et mouvement du cube
│   │   └── GameplaySnapshotAdapter.cs   Application du snapshot après initialisation
│   ├── Localization/
│   │   └── GameText.cs                   Adaptation des textes aux String Tables officielles
│   ├── Menu/
│   │   └── GameMenuController.cs         Start, menu, slots, crédits et sortie contrôlée
│   ├── Save/
│   │   ├── GameSaveController.cs         Vue Save/Load/Delete et capture/application
│   │   └── GameSaveData.cs               DTO, schéma et validation propres au jeu
│   └── UI/
│       ├── GameOptionsController.cs      Brouillon, onglets, confirmations et rebinding
│       ├── GameSettingsController.cs     Raccordement de la vue aux services Settings/Audio
│       ├── GameSettingsController.Graphics.cs  Partie graphique du même brouillon
│       ├── GameSettingsController.Dropdowns.cs Sélecteurs natifs et choix graphiques disponibles
│       ├── GameGraphicsCategories.cs     Sections empilées Affichage, Image et Détails
│       ├── GameGraphicsScroll.cs         Visibilité de la sélection dans la liste Graphics
│       ├── GameChoiceButton.cs           Choix au clic, Submit ou gauche/droite
│       ├── GameInputPresentation.cs      Prompts selon périphérique et bindings
│       ├── GameCursorService.cs          Visibilité du curseur pendant la session Bootstrap
│       ├── GameButtonGroup.cs            Largeur commune d'un groupe de boutons
│       ├── GameRebindRow.cs              Ligne de binding et focus
│       ├── GameSaveSlotCard.cs           Présentation d'un slot
│       └── GameSlotActivation.cs         Distinction clic pointeur / Submit
├── Editor/
│   ├── Lumivale.Start.Game.Editor.asmdef  Relais réservé à l'Editor
│   ├── GameEditorExit.cs                 Arrêt de Play après shutdown contrôlé
│   └── Build/
│       ├── Lumivale.Start.Game.Build.Editor.asmdef
│       └── ReleaseBuild.cs               Builds explicites des deux scènes actives
├── Input/
│   └── GameControls.inputactions         Maps Gameplay, UI et Common
├── Prefabs/
│   ├── GameCanvas.prefab                 Interface approuvée
│   ├── GameButton.prefab                 Bouton commun
│   └── GameBindingCell.prefab            Variante de bouton pour le rebinding
├── Audio/
│   ├── LumiDemo.mixer                    Groupes Master, Music et SFX
│   ├── DemoMusic.wav                     Boucle musicale temporaire
│   ├── UiClick.wav                       Retour sonore UI
│   ├── Gameplay2D.wav                    Son de gameplay non spatialisé
│   ├── Gameplay3D.wav                    Son spatialisé du cube
│   └── README.md                        Provenance, format et droits d'utilisation
├── Localization/
│   ├── LocalizationSettings.asset        Initialisation asynchrone, préchargement et fallback
│   ├── Locales/                          Français (fr) et English (en)
│   ├── Tables/                           GameUI, Shared Data et les deux String Tables
│   └── Addressables/                     Configuration et groupes locaux officiels
└── Tests/
    ├── Editor/                         Préférences et sessions Play répétées
    │   ├── Lumivale.Start.Game.Tests.Editor.asmdef
    │   ├── GamePreferencesTests.cs
    │   ├── GameGraphicsPresetsTests.cs
    │   ├── GameLocalizationTests.cs
    │   └── GamePlaySessionTests.cs
    └── PlayMode/                       Intégrations Menu, Options, navigation et saves
        ├── Lumivale.Start.Game.Tests.PlayMode.asmdef
        ├── GameMenuIntegrationTests.cs
        ├── GameOptionsIntegrationTests.cs
        ├── GameOptionsIntegrationTests.Graphics.cs
        ├── GameOptionsIntegrationTests.Localization.cs
        ├── GameOptionsIntegrationTests.Cursor.cs
        ├── GameOptionsIntegrationTests.Consistency.cs
        ├── GameLifecycleIntegrationTests.cs
        ├── GameBootstrapLifetimeTests.cs
        └── GameSaveAudioIntegrationTests.cs
~~~

Le runtime actif utilise le namespace et l'assembly `Lumivale.Start.Game`.
Les dossiers séparent les responsabilités sans multiplier les assemblies.
`GameSettingsController` raccorde les widgets Settings/Audio ; `GameOptionsController`
gère le parcours Options, les onglets et préférences complémentaires.
`GameGraphicsCategories` regroupe les sélecteurs du même brouillon en trois sections
empilées, chacune avec son titre, et conserve une navigation continue entre elles.
`GameCursorService` dépend d'Input et appartient au Bootstrap persistant : il
adapte la visibilité au périphérique actif et restaure son état initial à l'arrêt,
sans modifier le verrouillage ni enregistrer de préférence.

La collection `GameUI` centralise les traductions anglaises/françaises. Les labels
fixes du Canvas utilisent `LocalizeStringEvent` ; les contrôleurs dynamiques
passent par `GameText`. `GameLocalizationService` utilise la locale officielle et
le champ Settings existant, sans dépendance Localization dans le Core.

Les attributs `MovedFrom` rappellent d'anciens types Product ou Phase2/3/4 pour la
compatibilité. Ils ne nécessitent pas leurs anciennes assemblies. Les noms
`DemoMusic`, `LumiDemo` et `GameDemo` désignent toujours des assets utilisés.

## Configuration et documentation

~~~text
Lumi-start/
├── Assets/
│   ├── Game/                            Sources et assets actifs décrits ci-dessus
│   ├── Settings/                        Pipeline, renderers, volumes et réglages URP
│   └── TextMesh Pro/                    Polices, matériaux, shaders et ressources texte
├── Packages/
│   ├── manifest.json                    Packages, Core local et tests du package exposés
│   └── packages-lock.json               Versions résolues par Unity
├── ProjectSettings/                     Configuration Player, Editor, rendu, audio et build
├── Documentation/
│   ├── ARBORESCENCE_PROJET.md            Présent guide de l'organisation
│   ├── ARCHITECTURE.md                  Dépendances, contrats et compatibilité
│   ├── NEW_GAME_GUIDE.md                 Adapter une copie du Starter à un autre jeu
│   ├── BUILD_AND_TEST.md                 Commandes, suites et protection des données
│   ├── GRAPHICS_AND_PERFORMANCE.md       Options moteur, presets, DX11/DX12 et limites
│   ├── LOCALIZATION.md                   Langues, tables, brouillon, typographie et Addressables
│   ├── MANUAL_PLAYER_CHECKLIST.md        Validation humaine du Player et du matériel
│   └── POLISH_MANUAL_CHECKLIST.md        Revue humaine du parcours UI approuvé
├── README.md                            Installation et démarrage
├── AGENTS.md                            Règles de contribution
├── CHANGELOG.md                         Historique synthétique des changements
├── .gitignore                           Caches, builds et fichiers locaux exclus
└── .gitattributes                       Règles Git de traitement des fichiers
~~~

Les réglages sous `Assets/Settings` concernent le rendu URP. Ils sont distincts du
module Core Settings, qui gère les préférences utilisateur.
Seules GameMenu et GameDemo figurent dans le build, dans cet ordre.
Aucun builder historique ni ancienne scène de démonstration n'est requis.

## Package réutilisable

~~~text
lumi-core/
├── package.json                         com.lumivale.lumi-core 0.1.0
├── Runtime/
│   ├── Bootstrap/                       Cycle de vie et dépendances
│   ├── SceneFlow/                       Chargements et transitions
│   ├── Input/                           Contextes, abonnements et rebinding
│   ├── UI/                              Vues, modales, focus et pause
│   ├── Settings/                        Préférences et application graphique
│   ├── Audio/                           Musique, SFX et volumes
│   └── Persistence/                     Slots, sérialisation et fichiers
├── Tests/                               Tests des services, indépendants des assets Game
├── Documentation~/                      Installation, composition et mise à jour
├── README.md
├── AGENTS.md
└── CHANGELOG.md
~~~

Le Core ne référence pas le Starter. Chaque module possède son assembly et son
contrat ; voir [sa configuration](../../lumi-core/Documentation~/GETTING_STARTED.md).

## Données et dossiers générés

Les sauvegardes utilisent `Application.persistentDataPath/LumiSaves/game-lumivale-lumi-start-demo`,
le modèle `starter-cube` version `1` et les slots `demo-1` à `demo-3`.
Les préférences utilisent PlayerPrefs, clé `com.lumivale.lumi-start.preferences`
et documents `.presentation.v1` / `.bindings.v1`. Elles ne sont pas dans Assets.

`Library`, `Temp`, `Logs` et `UserSettings` sont locaux et générés.
`Builds` contient les sorties ignorées, dont les futurs rapports sous `Builds/Reports`.
Les `.meta`, scènes, prefabs, asmdefs, manifestes et configurations conservés font
partie des sources reproductibles. Ne pas supprimer de données utilisateur pour
restaurer une version de sources ou pour faire réussir un test.

| Besoin | Emplacement |
| --- | --- |
| Comprendre le démarrage et les services | Game/Runtime/Bootstrap |
| Adapter menus, options et prompts | Game/Runtime/Menu et UI |
| Traduire une chaîne ou ajouter une langue | Game/Localization et Documentation/LOCALIZATION.md |
| Adapter les données d'une partie | Game/Runtime/Save et GameplaySnapshotAdapter |
| Modifier les actions d'une copie de jeu | Game/Input/GameControls.inputactions |
| Adapter le visuel lors d'une mission autorisée | Game/Prefabs ; aucun générateur nécessaire |
| Tester l'intégration du Starter | Game/Tests |
| Tester/configurer un module réutilisable | lumi-core/Tests et README du module |
