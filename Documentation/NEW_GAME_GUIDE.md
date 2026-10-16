# Créer un jeu à partir de Lumi Start

Le Starter Unity **6000.3.13f1** compose les sept modules de
`com.lumivale.lumi-core` **0.1.0**. Le package fournit les services réutilisables ;
`Assets/Game` possède la composition, les assets et le modèle de partie.

Les adaptations décrites ici concernent une copie indépendante du Starter.
Modifier les assets existants dans Unity en conservant leurs métadonnées ; aucune
génération préalable des scènes ou prefabs n'est nécessaire.

## Préparer une copie indépendante

Conserver `Assets`, `Packages`, `ProjectSettings` et leurs métadonnées. Laisser Unity
régénérer Library, Temp, Logs et les fichiers IDE. Placer le package voisin sous le
nom `lumi-core` et conserver la dépendance locale :

~~~json
"com.lumivale.lumi-core": "file:../../lumi-core"
~~~

Le chemin est relatif au dossier `Packages`. Aucun script Core ne doit être copié
dans `Assets`. Pour un projet vierge, suivre le
[guide Core](../../lumi-core/Documentation~/GETTING_STARTED.md) puis ajouter les
références aux assemblies des modules utilisés.

Choisir un Company Name/Product Name propres au nouveau jeu, ainsi qu'une clé
Settings et une identité Persistence distinctes. Dans le Starter actuel, conserver
`com.lumivale.lumi-start.preferences`, `lumivale-lumi-start-demo` et les contrats
de données existants : le rangement de fichiers n'est pas une migration de saves.

## Composition : GameBootstrap

Le point d'entrée est
[GameBootstrap.cs](../Assets/Game/Runtime/Bootstrap/GameBootstrap.cs), namespace
et assembly `Lumivale.Start.Game`. Son GameObject racine est dédié et persiste
entre les scènes. Les champs sérialisés référencent les actions, groupes/mixer,
clips, préférences et configuration Persistence.

Pour composer un hôte créé par code, configurer son GameObject encore inactif avec
`Configure`, `ConfigureSettingsAudio` et `ConfigurePersistence` avant son Awake.
Ces méthodes refusent une composition déjà démarrée. Attendre `Ready` avant
d'accéder aux services.

Pour une autre composition, dériver `LumiBootstrap` et fournir les services dans
`CreateServices()`. Déclarer les dépendances entre services plutôt que compter sur
l'ordre des callbacks MonoBehaviour. Les scènes, assets et DTO restent dans le jeu.

[GameSettingsAudioBinding.cs](../Assets/Game/Runtime/Bootstrap/GameSettingsAudioBinding.cs)
dépend de `settings` et `audio` : il précharge les clips, applique les réglages et
volumes, s'abonne aux changements puis démarre la musique. Core Settings et Core
Audio restent indépendants. L'arrêt inverse retire la liaison avant ces services.
Attendre `ShutdownAsync()` avant une sortie volontaire.

## Scènes et navigation

Le parcours actif utilise uniquement :

- `Assets/Game/Scenes/GameMenu.unity` : Start screen et menu.
- `Assets/Game/Scenes/GameDemo.unity` : gameplay, pause et sauvegardes.

Les routes sont dans
[GameScenePaths.cs](../Assets/Game/Runtime/Configuration/GameScenePaths.cs).
`GameSceneController` raccorde les vues, le bridge Input et la navigation.
`GameplaySnapshotAdapter` applique un snapshot lorsque les contrôleurs de
destination sont prêts.

Dans un nouveau jeu, déplacer ou dupliquer les assets avec Unity et adapter
ensemble routes, références sérialisées et liste explicite des scènes commerciales.
Le builder utilise uniquement les deux scènes actives. Les démonstrations
historiques et leurs builders ont quitté le projet ; aucune étape de génération
ancienne n'est nécessaire pour obtenir un Starter fonctionnel.

Scene Flow refuse les chevauchements et attend la fin des opérations moteur.
Les transitions du Starter suspendent Input, ferment les vues, reprennent Pause
et relâchent la suspension après la registration de la destination. Une annulation
peut laisser la scène destination active ; refléter l'état réellement chargé.

## Input et interface

Les assets actifs sont `Assets/Game/Input/GameControls.inputactions` et
`Assets/Game/Prefabs/GameCanvas.prefab`, avec `GameButton.prefab` et
`GameBindingCell.prefab`. Adapter les assets existants dans Unity ; leur import
et leur utilisation ne nécessitent aucun générateur de présentation.

Les maps Input actuelles sont `Gameplay`, `UI` et `Common`. Gameplay active
Gameplay/Common ; UI et Pause activent UI/Common. `InputService` possède un clone
runtime. Conserver les IDs des actions et bindings ainsi que les chemins attendus
par les contrôleurs, le rebinding et `UiActionPaths`. Les préférences du Starter
persistent uniquement les overrides Gameplay ; Common reste réservé.

`GameOptionsController` possède le brouillon, les onglets et le rebinding.
`GameSettingsController` raccorde les valeurs aux services Settings/Audio.
`GameInputPresentation` adapte les prompts au périphérique et aux bindings effectifs.

Conserver un `UiScreen`/CanvasGroup par vue, des IDs uniques et un premier Selectable
cohérent. Le bridge EventSystem se lie après Ready au clone Input. Retirer les
registrations et callbacks lors de la désactivation. Les contrôleurs diffèrent les
changements de contexte jusqu'à Update pour laisser finir les callbacks Input natifs.
Pause/Options/Saves utilisent du temps non affecté par `Time.timeScale`.

## Settings et Audio

Les défauts graphiques viennent de `GameGraphicsBackend` et de `GameGraphicsPresets`.
Une qualité persistée est identifiée par son nom. Le menu actif demande confirmation
sur Back si le brouillon a changé ; Apply valide, applique et enregistre, Reset
modifie le brouillon sans sauvegarde implicite. Language dans Interface prévisualise
la locale officielle ; Apply confirme ce choix, tandis que l'abandon restaure la
langue appliquée. Adapter les textes et locales sous `Assets/Game/Localization`
en suivant [le guide de localisation](LOCALIZATION.md).

Le mixer, les quatre WAV et leur provenance sont dans `Assets/Game/Audio`.
Le mixer possède les groupes Master, Music et SFX, avec volumes exposés. Précharger
les clips et appliquer les volumes avant la première lecture ; fournir un listener
dans les scènes. Pour une composition C#, utiliser si nécessaire l'alias
`using AudioConfiguration = Lumivale.Core.Audio.AudioConfiguration;`.

Les documents complémentaires de `GamePreferences` conservent les suffixes
`.presentation.v1` et `.bindings.v1` de la clé Settings. Ils sont séparés du document
Settings existant et des sauvegardes de gameplay.

## Modèle de partie et sauvegardes

Le DTO actuel est
[GameSaveData.cs](../Assets/Game/Runtime/Save/GameSaveData.cs), modèle `starter-cube`,
version `1`. `GameSaveController` capture et applique les données de scène ;
`GameSessionState` transporte le snapshot validé entre menu et gameplay.

Renommer une classe/namespace ne change pas son contrat JSON. Conserver les champs
`PositionX`, `PositionY`, `PositionZ` et `Counter`, l'identité de jeu, le modèle,
la version et les slots `demo-1` à `demo-3` pour lire les données existantes.

Pour un nouveau jeu indépendant, définir son propre DTO et schéma dans son projet :

~~~csharp
using System;
using Lumivale.Core.Persistence;

[Serializable]
public sealed class MyGameSave
{
    public bool HasSnapshot;
    public float PositionX;
    public int Counter;

    public static SaveSchema<MyGameSave> Schema()
    {
        return new SaveSchema<MyGameSave>("checkpoint", 1, data =>
        {
            if (data == null || !data.HasSnapshot || data.Counter < 0 ||
                float.IsNaN(data.PositionX) || float.IsInfinity(data.PositionX))
                throw new ArgumentException("Invalid gameplay save.");
        });
    }
}
~~~

Capturer les objets sur le thread Unity, puis appeler
`SaveAsync("slot-1", snapshot, MyGameSave.Schema(), token)` sur le service Persistence.
Load retourne un DTO ; appliquer les données seulement si `IsSuccess` et si la
vue/scène propriétaire est encore valable. Afficher les diagnostics et
`RecoveredFromBackup`. Confirmer Delete et préserver un principal corrompu ou
un format futur jusqu'à une décision explicite.

Lors d'un changement de contrat, préparer une migration dans le jeu avec une
nouvelle version et validation des données converties. Les DTO restent des classes
Serializable concrètes à champs supportés, sans références Unity, dictionnaires,
polymorphisme ou callbacks de sérialisation. Voir les
[garanties et limites Persistence](../../lumi-core/Runtime/Persistence/README.md).

## Tests et livraison

Les suites actives sont `Lumivale.Start.Game.Tests.Editor` et
`Lumivale.Start.Game.Tests.PlayMode`. Les tests propres aux services réutilisables
restent dans `../lumi-core/Tests`. Le manifeste consommateur expose les tests de
package avec `testables: ["com.lumivale.lumi-core"]`.

Consulter [Build et tests](BUILD_AND_TEST.md) pour les entrées actuelles et
l'[architecture](ARCHITECTURE.md) pour les contrats de compatibilité.

Un test Input avec périphérique virtuel, une observation Audio ou une compilation
Editor ne valident pas à eux seuls un Player cible. Pour une livraison ultérieure,
utiliser une identité de validation isolée et renseigner la
[checklist matérielle](MANUAL_PLAYER_CHECKLIST.md).
