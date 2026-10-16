# Lumi Start

Starter Unity de Lumivale Studio : **Unity 6000.3.13f1**, rendu URP et package local
`com.lumivale.lumi-core` **0.1.0**. Le jeu, sa composition et ses tests se trouvent
dans **`Assets/Game`**.

## Installation locale

Récupérer les deux dépôts privés avec un compte autorisé et les placer côte à côte.
Utiliser les URLs fournies par Lumivale Studio et conserver le nom de dossier
`lumi-core` pour le package :

~~~text
workspace/
├── lumi-start/
└── lumi-core/
~~~

Dans Unity Hub, ajouter le dossier `lumi-start`, puis l'ouvrir avec Unity
**6000.3.13f1**. Attendre la résolution des packages et la compilation. Le manifeste
`Packages/manifest.json` utilise :

~~~json
"com.lumivale.lumi-core": "file:../../lumi-core"
~~~

Le chemin est relatif au dossier `Packages`. Sur un système sensible à la casse,
nommer le dossier voisin exactement `lumi-core`. Aucun script Core n'est recopié
dans `Assets`. Conserver le manifeste, son verrou UPM et les deux versions de
sources utilisées ensemble : une dépendance locale ne verrouille pas un commit.

Ouvrir [GameMenu.unity](Assets/Game/Scenes/GameMenu.unity), puis lancer Play.
[GameDemo.unity](Assets/Game/Scenes/GameDemo.unity) contient le gameplay, la pause
et les sauvegardes. Ces deux scènes, dans cet ordre, constituent le build du jeu.

## Organisation

- `Assets/Game/Runtime` : Bootstrap, Configuration, Gameplay, Localization, Menu, Save et UI,
  dans l'assembly et le namespace `Lumivale.Start.Game`.
- `Assets/Game/Scenes`, `Prefabs`, `Input`, `Audio`, `Localization` : assets du parcours actif.
- `Assets/Game/Editor` : sortie contrôlée de Play et commandes de build.
- `Assets/Game/Tests` : intégrations Editor et PlayMode du Starter.
- `Assets/Settings` et `Assets/TextMesh Pro` : ressources de rendu et de texte conservées.
- `../lumi-core` : sept services techniques réutilisables, contrats et tests propres.

Le sens des dépendances est **Game → Core et packages Unity**. Le Core ne connaît
ni le Canvas, ni les scènes, ni le modèle de partie du Starter. Il n'existe pas
de deuxième composition historique ou de générateur nécessaire au démarrage.

## Parcours et données

Le Start screen ouvre le menu avec Espace ou A. New game choisit un slot vide ;
Continue game affiche les trois slots, leurs dates et leurs diagnostics. Un
premier clic sélectionne un slot et un second clic sur le même slot occupé charge
sa sauvegarde. Entrée/A charge directement un slot occupé. Delete est confirmé.
Le Starter n'effectue aucun autosave.

Options propose Gameplay, Controls, Graphics, Audio et Interface. Apply valide,
applique et enregistre le brouillon ; Back demande confirmation des changements.
Graphics regroupe ses treize réglages en sections empilées Affichage, Image et Détails,
avec un titre au-dessus de chaque groupe dans la même zone défilante. Les listes
déroulantes affichent « Nom : valeur » ; ouvrir la liste ou y naviguer conserve le
brouillon, puis un choix explicite sélectionne la valeur disponible. Language
utilise cette même présentation. Les retours de rebinding et diagnostics restent
dans la zone de statut commune des Options. Les prompts suivent le périphérique
et les bindings effectifs ; la manette masque le curseur, une activité clavier ou
souris le réaffiche. Le survol souris est également bloqué en mode manette.
Cette règle persiste entre les scènes et restaure l'état
initial à l'arrêt du Bootstrap. Credits défile en temps non affecté par la pause.
Exit attend l'arrêt des services avant de quitter.

L'interface est disponible en français et en anglais via Unity Localization
1.5.13. Language dans Interface prévisualise la langue ; Apply l'enregistre dans
le document Settings existant et l'abandon du brouillon restaure la précédente.
Au premier lancement sans préférence valide, le système français choisit Français,
les autres langues choisissent English. Les détails sont dans le
[guide de localisation](Documentation/LOCALIZATION.md).

[GameBootstrap.cs](Assets/Game/Runtime/Bootstrap/GameBootstrap.cs) compose les
modules Core. Settings et Audio restent indépendants ; leur synchronisation est
assurée par `GameSettingsAudioBinding`. Les actions et sons appartiennent au jeu.

[GameSaveData.cs](Assets/Game/Runtime/Save/GameSaveData.cs) définit le modèle
`starter-cube`, version `1`, avec `PositionX`, `PositionY`, `PositionZ` et `Counter`.
Les slots `demo-1` à `demo-3` utilisent
`Application.persistentDataPath/LumiSaves/game-lumivale-lumi-start-demo`.
Settings conserve la clé `com.lumivale.lumi-start.preferences` et les documents
complémentaires `.presentation.v1` et `.bindings.v1`. Le rangement du projet ne
change ni ces contrats ni les données existantes.

## Guides

- [Architecture actuelle et arborescence](Documentation/ARBORESCENCE_PROJET.md).
- [Créer un nouveau jeu](Documentation/NEW_GAME_GUIDE.md).
- [Build et tests](Documentation/BUILD_AND_TEST.md).
- [Configuration et contrats du Core](../lumi-core/Documentation~/GETTING_STARTED.md).
- [Checklist du Player](Documentation/MANUAL_PLAYER_CHECKLIST.md) et
  [revue des menus](Documentation/POLISH_MANUAL_CHECKLIST.md).
- [Architecture, compatibilité et limites](Documentation/ARCHITECTURE.md).
- [Graphics, presets, performances et compatibilité Windows](Documentation/GRAPHICS_AND_PERFORMANCE.md).
- [Localisation, langues, tables et textes dynamiques](Documentation/LOCALIZATION.md).

## Exécuter les tests

Ouvrir **Window > General > Test Runner** dans Unity. Choisir **EditMode** pour
les préférences et les tests de sessions, ou **PlayMode** pour les intégrations
Menu, Options, navigation et sauvegardes. Sélectionner la suite concernée puis
cliquer **Run Selected**. Les suites Starter sont :

- `Lumivale.Start.Game.Tests.Editor`.
- `Lumivale.Start.Game.Tests.PlayMode`.

Le manifeste inclut `"testables": ["com.lumivale.lumi-core"]` pour exposer les
tests du package voisin. Les procédures détaillées et la protection des données
sont décrites dans [Build et tests](Documentation/BUILD_AND_TEST.md).
Les tests virtuels ne remplacent pas les périphériques physiques, l'écoute et la
revue humaine du Player cible.

## Conservation des sources

Le Starter et le Core ont deux dépôts Git indépendants. Conserver sources,
`.meta`, scènes, prefabs, asmdefs, manifestes, verrou UPM et ProjectSettings.
Les caches Unity, sorties de build, sauvegardes utilisateur, credentials et
fichiers propres à une machine restent exclus du versionnement.

La version reste candidate à une validation de release. La synchronisation d'un
dépôt privé ne constitue pas une release finale.
