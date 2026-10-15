# Build Windows et tests

Ce guide décrit les commandes du Starter **Unity 6000.3.13f1**.
Une procédure n'est pas un résultat d'exécution. Choisir les vérifications selon
la modification et conserver les résultats du passage réellement exécuté.

## Scènes et outil de build

[ReleaseBuild.cs](../Assets/Game/Editor/Build/ReleaseBuild.cs), classe
`Lumivale.Start.Game.Editor.ReleaseBuild`, appartient à l'assembly
`Lumivale.Start.Game.Build.Editor`. Sa liste de scènes est explicite :

1. `Assets/Game/Scenes/GameMenu.unity`.
2. `Assets/Game/Scenes/GameDemo.unity`.

Build Settings doit contenir ces deux scènes, dans cet ordre. Les anciennes
démonstrations et la scène de template ne sont plus des entrées de validation.

| Méthode statique | Usage | Sortie ignorée |
| --- | --- | --- |
| `BuildWindowsMono()` | Windows x64 Mono | `Builds/Windows/Mono` |
| `BuildWindowsValidation()` | Mono Development, identité isolée | `Builds/Windows/Validation` |
| `BuildWindowsIl2Cpp()` | Windows x64 IL2CPP, toolchain requise | `Builds/Windows/Il2Cpp` |

Les exécutables sont `LumiStart.exe` et `LumiStartValidation.exe`.
Les rapports générés sont `Builds/Reports/Build-Mono.json`, `Build-Validation.json`
et `Build-Il2Cpp.json`. Ces sorties ignorées ne font pas partie des sources.

Le builder restaure backend, stripping, Company/Product, version, Development,
runInBackground, Pipeline et diagnostics après sa tentative. Il préserve aussi
les fichiers de réglages Unity que BuildPlayer peut réécrire. La garde des
diagnostics utilise une API interne Editor : la revoir lors d'une mise à jour
de Unity. Vérifier le rapport natif et les réglages après chaque tentative.
`GameEditorExit.cs` reste un relais Editor séparé du code du Player.

## Lancer un build

Les menus `Lumivale > Build` proposent les variantes Windows.
Pour une connexion Unity ouverte, appeler une seule méthode Queue :

~~~csharp
Lumivale.Start.Game.Editor.ReleaseBuild.QueueWindowsMono();
Lumivale.Start.Game.Editor.ReleaseBuild.QueueWindowsValidation();
Lumivale.Start.Game.Editor.ReleaseBuild.QueueWindowsIl2Cpp();
~~~

Queue retourne avant le build. Attendre le résultat de BuildPipeline et son JSON ;
une mise en file ou un dossier de sortie ne prouve pas un build réussi.

Pour un Editor distinct, fermer d'abord celui qui possède le projet. Exemple
PowerShell depuis la racine du Starter, après avoir défini `UNITY_EDITOR_PATH`
avec le chemin de l'exécutable Unity **6000.3.13f1** installé :

~~~powershell
$unityEditor = $env:UNITY_EDITOR_PATH
$projectPath = (Get-Location).Path
$buildLogDirectory = Join-Path $projectPath 'Builds/Windows'
New-Item -ItemType Directory -Path $buildLogDirectory -Force | Out-Null
& $unityEditor -batchmode -quit -projectPath $projectPath `
  -executeMethod Lumivale.Start.Game.Editor.ReleaseBuild.BuildWindowsMono `
  -logFile (Join-Path $buildLogDirectory 'Mono-build.log')
~~~

Changer le nom de méthode pour une autre variante. Ne pas ouvrir deux éditeurs
sur le même projet. Examiner code de sortie, BuildReport, messages et fichiers.
IL2CPP exige son module Unity et une toolchain C++/SDK disponible.

## Compiler et choisir les tests

Le manifeste conserve Unity Test Framework **1.6.0**, les packages déjà installés,
la dépendance locale Core et l'entrée racine :

~~~json
"testables": ["com.lumivale.lumi-core"]
~~~

Dans Unity, attendre la fin de la compilation et contrôler **Window > General >
Console**. Ouvrir **Window > General > Test Runner**, choisir **EditMode** ou
**PlayMode**, sélectionner la suite concernée et utiliser **Run Selected**.
Les tests du Core sont visibles grâce à l'entrée `testables` du manifeste.

Avec Unity CLI/MCP, appeler `recompile`, puis `recompile_status` et contrôler la
console. Inventorier les tests avant de choisir le filtre correspondant au changement.

| Suites actives | Mode MCP | Assembly |
| --- | --- | --- |
| Préférences et sessions Play du Starter | `editor` | `Lumivale.Start.Game.Tests.Editor` |
| Menu, Options, scènes et sauvegardes | `playmode` | `Lumivale.Start.Game.Tests.PlayMode` |

Pour un cas ciblé, utiliser son nom découvert dans le filtre `filter`.
Pour sélectionner une assembly via MCP, exemple :

~~~json
{
  "mode": "editor",
  "filter_type": "assembly",
  "filter": "Lumivale.Start.Game.Tests.Editor",
  "async_tests": true,
  "timeout": 120
}
~~~

Récupérer `test_status` jusqu'au résultat final, y compris après une interruption
de transport pendant un reload. Conserver noms, statuts, durées et messages.
Une réponse `started` ou un timeout de transport ne prouve pas un succès.
Un test retiré avec une démonstration ne doit plus figurer dans le bilan actuel.

Les tests propres aux services réutilisables restent dans `../lumi-core/Tests`.
Sélectionner leur assembly détectée pour le module concerné. Ne copier aucun
test Core dans le Starter pour recréer une suite historique.
Les fixtures de sessions entrent réellement en Play sans Domain Reload,
attendent Shutdown et restaurent les réglages qu'elles possèdent.

## Contrôles ciblés et données

Après une modification structurelle, inspecter les deux scènes et les prefabs :
aucun Missing Script, références attendues, assemblies sans dépendance historique,
Bootstrap unique, UI et sauvegardes raccordées. Effectuer le parcours concerné.
Ne pas régénérer le Canvas pour vérifier ses références.

Les fixtures doivent utiliser des racines, clés et périphériques virtuels isolés,
puis les retirer, restaurer les réglages et attendre l'arrêt des services.
Ne jamais appeler PlayerPrefs.DeleteAll ni supprimer les sauvegardes réelles.

Le Player normal utilise l'identité du produit. La variante isolée conserve
Company **Lumivale Studio Validation**, Product **Lumi-start Phase5 Validation** :
ce nom existant est volontairement conservé pour ne pas changer son stockage.
Vérifier le `persistentDataPath` réel avant une opération Save/Delete et préserver
les données préexistantes de cette identité de validation.

Le Starter garde `lumivale-lumi-start-demo`, le modèle `starter-cube` version `1`,
les slots `demo-1` à `demo-3` et `com.lumivale.lumi-start.preferences`, avec
`.presentation.v1` et `.bindings.v1`. Aucun nettoyage de sources ne les modifie.
Ne pas versionner de descripteur Pipeline contenant un token.

Distinguer compilation, tests virtuels, Player natif, périphériques physiques
et écoute humaine. Pour une future validation matérielle, utiliser la
[checklist Player](MANUAL_PLAYER_CHECKLIST.md) et la
[revue des menus](POLISH_MANUAL_CHECKLIST.md). Les anciens builds archivés
ne valident pas les scènes et sources actuelles.
