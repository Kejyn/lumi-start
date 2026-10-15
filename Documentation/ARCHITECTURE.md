# Architecture et compatibilité de Lumi Start

Lumi Start est un projet **Unity 6000.3.13f1** qui consomme le package UPM
`com.lumivale.lumi-core` **0.1.0**. Les responsabilités du jeu se trouvent sous
`Assets/Game`, dans l'assembly et le namespace `Lumivale.Start.Game`.

## Frontière entre le jeu et le package

Le sens des dépendances est **Game → Core et packages Unity**. Le Core fournit les
services ; Game possède les scènes, le style, les actions, le mixer, les sons,
les DTO et les règles de sauvegarde. Le package ne référence aucun asset Starter.
Le code Core reste dans son dépôt voisin, consommé par `file:../../lumi-core`.

| Module Core | Responsabilité | Dépendances Core |
| --- | --- | --- |
| Bootstrap | Graphe de services, démarrage déterministe, rollback et arrêt inverse | Aucune |
| Scene Flow | Navigation asynchrone, transitions, progression et exclusivité | Bootstrap |
| Input | Clone des actions, contextes, suspensions, périphériques et rebinding | Bootstrap |
| UI | Vues uGUI, modales, focus, bridge Input System et pause | Bootstrap, Input |
| Settings | Préférences validées, stockage PlayerPrefs et application explicite | Bootstrap |
| Audio | Mixer fourni par le jeu, musique, fondus et pool SFX borné | Bootstrap |
| Persistence | Slots, schémas, diagnostics, intégrité et fichiers avec backup | Bootstrap |

Chaque module Core possède son assembly. Settings et Audio ne se référencent pas ;
`GameSettingsAudioBinding` applique les préférences avant de démarrer la musique.
Les contrats détaillés sont dans le
[guide Core](../../lumi-core/Documentation~/GETTING_STARTED.md) et ses README de modules.

## Composition et scènes

`GameBootstrap` compose les services sur un hôte racine persistant et protégé
contre les initialisations multiples. Les contrôleurs attendent `Ready` avant
d'accéder aux services. `GameSceneController` raccorde navigation, Input et pause.
`GameSessionState` transporte l'état de session et le snapshot sélectionné ;
`GameplaySnapshotAdapter` l'applique lorsque la destination est prête.

Les seules scènes du build sont `GameMenu`, puis `GameDemo`.
Editor, Build et tests utilisent des assemblies séparées du runtime Player.
L'interface repose sur les prefabs existants, sans étape de génération préalable.
Voir [l'arborescence](ARBORESCENCE_PROJET.md) pour les fichiers et leurs rôles.

## Identités et compatibilité des données

Settings stocke les préférences ; Persistence stocke les parties. Les contrats du
Starter sont :

- Identité Persistence : `lumivale-lumi-start-demo`.
- Modèle : `starter-cube`, version `1`, champs `PositionX`, `PositionY`,
  `PositionZ` et `Counter` dans `GameSaveData`.
- Slots : `demo-1`, `demo-2`, `demo-3`.
- Clé Settings : `com.lumivale.lumi-start.preferences` ; documents complémentaires
  `.presentation.v1` et `.bindings.v1`.

Renommer une classe ne migre pas les données d'une partie. Toute évolution de ces
identités ou champs demande une décision de migration explicite. Un nouveau jeu
indépendant doit choisir ses propres identités afin d'isoler ses données.

Les GUID et noms des champs sérialisés Unity sont des contrats distincts du JSON.
Les attributs `MovedFrom` conservent les anciennes identités Product et
Phase2/3/4 des types déplacés vers Game ; ils ne créent pas une dépendance à ces
anciennes assemblies. Conserver ces attributs et les `.meta` lors de changements
sans migration intentionnelle des assets.

## Cycle de vie, tests et limites

Les APIs Unity, validations et captures DTO s'utilisent sur le thread principal.
Un chargement Unity commencé peut activer sa scène malgré l'annulation de la
demande. Attendre un arrêt explicite des services avant une sortie contrôlée :
Unity n'attend pas les continuations asynchrones de ses callbacks de fermeture.
Les abonnements de scène et résultats devenus obsolètes restent à nettoyer.

Persistence conserve une génération de backup et un contrat JsonUtility strict.
Il ne garantit ni transaction universelle, ni survie à toute coupure électrique,
ni chiffrement/authentification. La migration métier et les essais du filesystem
cible appartiennent au jeu. Les préférences complémentaires ne forment pas une
transaction multi-clés. Les collisions entre bindings Gameplay ne sont pas
résolues automatiquement.

Les tests de services réutilisables appartiennent au Core ; les intégrations Game
restent sous `Assets/Game/Tests`, avec données et périphériques isolés.
Voir [Build et tests](BUILD_AND_TEST.md). La compilation Editor et les périphériques
virtuels ne remplacent pas une validation du Player, de son backend, de l'écoute
et des périphériques physiques sur la plateforme cible.
