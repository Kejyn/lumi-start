# Localisation du Starter

Le parcours actif de `lumi-start` utilise le package officiel **Unity Localization
1.5.13**, publié pour Unity **6000.3**, avec uGUI et TextMeshPro. Le verrou UPM
résout Addressables **2.9.1** ; Newtonsoft **3.2.2**, déjà présent, est conservé.
Conserver `Packages/manifest.json`, `packages-lock.json`, les assets et leurs
`.meta` ensemble. [Compatibilité officielle Unity 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.localization.html).

Deux langues sont disponibles : **Français (`fr`)** et **English (`en`)**.
La collection `GameUI` contient **231 clés**, soit **462 traductions non vides**
au moment de cette livraison, pour les textes fixes, les informations dynamiques,
les confirmations, les sauvegardes et les prompts. Les noms des langues restent
dans leur propre langue. Les identifiants internes et noms propres ne sont pas
traduits. Les crédits de démonstration sont remplaçables par ceux du jeu.

## Ressources et responsabilités

```text
Assets/Game/
├── Localization/
│   ├── LocalizationSettings.asset
│   ├── Locales/                    en.asset et fr.asset
│   ├── Tables/
│   │   ├── GameUI.asset             Collection visible dans Localization Tables
│   │   ├── GameUI Shared Data.asset Identifiants partagés et clés stables
│   │   ├── GameUI_en.asset
│   │   └── GameUI_fr.asset
│   └── Addressables/               Configuration, groupes et builders officiels
└── Runtime/
    ├── Configuration/GameLocalizationService.cs
    └── Localization/GameText.cs
```

`LocalizationSettings.SelectedLocale` est l'autorité sur la langue affichée.
`GameLocalizationService`, composé par `GameBootstrap` après Settings, attend
l'initialisation native et le préchargement des tables, applique la préférence
du jeu et encadre son aperçu. `GameText` adapte les appels des contrôleurs aux
String Tables et aux composants officiels ; il ne contient aucun dictionnaire de
traductions ni moteur de changement de langue indépendant.

Le package Core conserve son champ générique `SettingsValues.LanguageCode` et
ses règles de validation. Il ne dépend pas de Unity Localization. Les textes,
locales et règles de premier lancement appartiennent au Starter.

## Premier lancement, préférence et brouillon

Le Bootstrap capture `Application.systemLanguage` à la construction des valeurs
par défaut : français donne `fr`, anglais donne `en`, toute autre valeur donne
`en`. Cette règle n'interroge pas le pays pour proposer une langue absente.
Une préférence linguistique valide déjà enregistrée est prioritaire. Un ancien
document contenant `en` reste donc en anglais, même sur un système français :
le programme ne peut pas déduire si cette valeur provenait d'un choix explicite.

La langue confirmée reste dans **le document Settings V1 existant**, champ
`LanguageCode`, sous la clé configurable du Bootstrap, par défaut
`com.lumivale.lumi-start.preferences`. Aucune clé `selected-locale` supplémentaire
et aucun `PlayerPrefLocaleSelector` n'enregistrent l'aperçu. Les documents
`.presentation.v1`, `.bindings.v1`, les identités Persistence, slots et schémas de
partie restent indépendants. L'ouverture du jeu ne force aucune réécriture
globale de ces documents.

Dans **Options → Interface → Language / Langue** :

- Le contrôle utilise le composant officiel `TMP_Dropdown`. Un premier clic, ou
  Submit au clavier/manette, ouvre la liste **English / Français**. L'ouverture
  et la navigation entre les éléments ne changent ni la langue ni le brouillon.
- Un second clic sur une langue, ou Submit sur l'élément choisi, sélectionne cette
  langue et demande immédiatement son aperçu via `SelectedLocale`, sans
  sauvegarde, changement de scène ou fermeture des Options. La liste se referme
  et le focus revient au contrôle Language / Langue.
- Apply confirme et sauvegarde le brouillon existant, langue comprise.
- Back ferme d'abord la liste si elle est ouverte. Une fois la liste fermée, Back
  conserve la confirmation de modifications ; abandonner restaure la langue
  confirmée, avec les autres valeurs du brouillon. Les raccourcis d'onglets ne
  changent pas le panneau derrière une liste ouverte.
- Reset sélectionne la langue par défaut issue du système, capturée pour ce
  Bootstrap. Il faut Apply pour la conserver ; Back peut annuler cet aperçu.

La légende du contrôle utilise `options.interface.language` pour afficher
**Nom : valeur**, par exemple « Langue : Français ». Les éléments de la liste
restent English et Français quelle que soit la locale. Le rafraîchissement du
contrôle utilise `SetValueWithoutNotify` pour éviter une sélection parasite au
démarrage, à la réouverture ou lors d'un changement de locale. L'ancien composant
`GameChoiceButton` de la ligne reste présent pour préserver ses références, mais
il est désactivé ; seul le dropdown est sélectionnable.

Les treize réglages Graphics utilisent aussi des dropdowns natifs avec une légende
localisée « Nom : valeur », répartis dans **Affichage**, **Image** et **Détails**.
Les éléments des listes affichent leurs valeurs, traduites lorsque leur sens le
demande ; FXAA, SMAA, MSAA et les valeurs techniques gardent leurs noms. Les choix
disponibles suivent les capacités du backend graphique existant. **Personnalisée**
est un état dérivé en lecture seule. Ouvrir ou parcourir une liste ne modifie pas
le brouillon ; sélectionner une valeur le met à jour, puis Apply applique et
enregistre les réglages. Ces contrôles conservent le document Settings V1 et les
clés existantes ; ils n'ajoutent aucun stockage de préférences.

Une application de locale peut terminer un préchargement natif asynchrone.
`PreviewReady` permet aux tests et à un appelant asynchrone d'attendre la dernière
demande. Le service termine l'opération en cours puis conserve seulement la
dernière demande reçue. Il ne change pas une seconde fois la locale au milieu
d'un chargement natif. Son arrêt attend ces opérations avant de restaurer la
locale précédente. Les composants retirent leurs abonnements à
`SelectedLocaleChanged` lorsqu'ils sont désactivés. Attendre l'arrêt du Bootstrap
dans les sessions et sorties contrôlées.

Les deux tables sont préchargées avec `PreloadAllLocales` ; l'initialisation
synchrone est désactivée. Les contrôleurs attendent le Bootstrap et l'interface
attend les textes requis avant de devenir visible. Un futur service ne doit pas
appeler `WaitForCompletion` pour afficher une chaîne ni réinitialiser globalement
Localization pendant que des vues l'utilisent. [Initialisation et changement de
locale officiels](https://docs.unity3d.com/Packages/com.unity.localization@1.5/api/UnityEngine.Localization.Settings.LocalizationSettings.html).

## Ajouter une chaîne ou traduire un écran

Ouvrir **Window → Asset Management → Localization Tables**, puis `GameUI`.
Ajouter une clé descriptive et stable, par exemple `inventory.empty`, avec une
valeur anglaise et française non vide. Réutiliser une clé commune si le sens et
le contexte sont identiques ; des homonymes employés dans des contextes différents
peuvent nécessiter des clés distinctes. Ne pas renommer les clés en fonction du
texte affiché et ne pas traduire une clé ou un chemin d'Input Action.

Pour un label fixe du prefab existant :

1. Ajouter le composant officiel `LocalizeStringEvent` au GameObject du texte TMP.
2. Choisir la collection `GameUI` et sa clé dans `String Reference`.
3. Relier `On Update String` au setter public `TMP_Text.text` du texte concerné.
4. Conserver un seul listener et autoriser son exécution en Editor et Runtime
   pour la prévisualisation d'édition ; demander `RefreshString` si la référence
   est configurée par script.

Un outil Editor peut employer `UnityEventTools.AddPersistentListener` avec un
`UnityAction<string>` créé sur le setter public `set_text`, puis
`SetPersistentListenerState(..., EditorAndRuntime)`. Ne pas écrire les champs
privés sérialisés des UnityEvents. Enregistrer les assets modifiés et vérifier
la cible, la méthode et la mise à jour réelle après rechargement.
[LocalizeStringEvent](https://docs.unity3d.com/Packages/com.unity.localization@1.5/api/UnityEngine.Localization.Components.LocalizeStringEvent.html).

Pour traduire un écran, examiner **ses composants et son code** : titres et
boutons, mais aussi branches conditionnelles, erreurs, confirmations, valeurs
de réglages et états de chargement. Le label d'un contrôleur dynamique ne doit
pas recevoir en parallèle un binding fixe qui écraserait sa valeur. Préserver
les positions, tailles, styles et animations approuvés ; choisir des traductions
concises. Signaler un dépassement qui exige un changement visuel plutôt que de
modifier globalement le layout ou les polices.

## Textes dynamiques et prompts

Utiliser les clés dans les contrôleurs, puis laisser les mécanismes officiels
formater les paramètres :

```csharp
GameText.Set(statusLabel, "save.available");
string title = GameText.Text("save.slot_title", slotNumber, translatedState);
```

`GameText.Set` conserve une référence `LocalizedString` et un
`LocalizeStringEvent` sur le label. `GameText.Text` lit une entrée déjà préchargée,
utile pour un fragment ou une composition. Si un contrôleur assemble des
fragments ou une date, il doit les recalculer sur `SelectedLocaleChanged` et
retirer cet abonnement à sa désactivation. Ne pas conserver un résultat traduit
dans un cache indépendant de la locale. Avant initialisation ou pour une clé
absente, le helper peut afficher `[clé]` : c'est un diagnostic à corriger.

Les messages numériques ou composites utilisent actuellement les paramètres
`{0}`, `{1}`, etc., via le formatage standard du package. Les entrées concernées
peuvent activer **Smart** pour les futurs formats et règles de pluriel nécessaires ;
conserver les mêmes paramètres dans les deux langues.
Éviter de concaténer des fragments dont l'ordre dépend de la grammaire. Les dates
de sauvegarde utilisent l'heure locale avec la culture de la locale sélectionnée ;
les données UTC enregistrées ne sont pas modifiées. [Smart Strings](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Smart/SmartStrings.html).

Les clés `prompt.*` restent **non Smart** : leurs tokens tels que
`{Gameplay/Move}` ou `{UI/StartContinue}` désignent des Input Actions, pas des
arguments linguistiques. `GameInputPresentation` traduit d'abord le modèle puis
résout ces tokens avec les bindings effectifs du périphérique actif. Les noms
d'actions internes restent inchangés. Les noms de touches visibles connus sont
traduits ; les symboles et libellés techniques fournis par un périphérique
inconnu restent ceux de l'Input System.

## Ajouter une langue

Espagnol (`es`), allemand (`de`), russe (`ru`) et chinois simplifié (`zh-Hans`)
ne sont pas créés ni proposés actuellement. Pour en ajouter une :

1. Créer sa Locale sous `Assets/Game/Localization/Locales` via les outils officiels.
2. Ajouter sa table à `GameUI` et traduire **toutes** les clés. Une table vide ou
   remplie de copies anglaises ne constitue pas une langue livrée.
3. Vérifier l'enregistrement Addressables, le préchargement et un fallback
   explicite sans cycle ; conserver l'anglais comme repli technique approprié.
4. Étendre la liste des langues de `SettingsConfiguration` dans `GameBootstrap`,
   les choix/autonymes de `GameSettingsController` et la normalisation de
   `GameLocalizationService`. Le code V0.1 gère explicitement `en` et `fr` : ajouter
   uniquement un asset Locale ne suffit pas. Étendre la détection système si voulu.
5. Vérifier chaque écran, les textes dynamiques, les glyphes, les trois modes
   d'entrée et les tests de premier lancement/persistance/brouillon.

La vérification de complétude doit parcourir chaque clé de Shared Data dans
chaque table disponible et refuser les valeurs absentes ou vides. Zéro entrée
examinée n'est pas un succès. Le test Editor
`GameLocalizationTests.EveryAvailableLocaleHasEveryNonEmptyTranslation` protège
ce contrat pour les langues livrées.

## Polices, Addressables et validation

Les polices TMP actuelles sont conservées pour français/anglais ; aucune grosse
police inutilisée n'est ajoutée. Les groupes de boutons localisés gardent la
mesure anglaise de référence pour préserver les cadres approuvés, plutôt que de
les élargir selon la langue. Un nouveau texte doit tenir dans ces cadres ; signaler
un débordement au lieu de changer globalement la police ou le layout. Dans le menu,
« New game » devient « Jouer » pour tenir dans le bouton approuvé ; « Nouvelle partie »
reste utilisé dans la sélection des sauvegardes, où la place est suffisante.
Avant le russe, vérifier les glyphes cyrilliques
sur une police dont les droits autorisent la redistribution. Un fallback TMP
ciblé peut compléter une couverture contrôlée, mais doit être testé après import
et dans le Player. Pour le chinois, préférer une police dédiée à la locale,
référencée par une **Asset Table** et les mécanismes officiels de localisation de
font TMP, avec atlas dynamique/multi-atlas approprié. Importer la source de police
et sa licence ; ne pas supposer qu'une police système est redistribuable. Tester
les variantes, accents, ponctuations et caractères absents sur les écrans réels.

Localization utilise Addressables pour découvrir et charger les locales/tables.
Conserver les groupes, labels, Shared Data et références GUID générés. Les tables
livrées utilisent des groupes locaux ; aucun serveur de traduction n'est requis.
[Organisation officielle avec Addressables](https://docs.unity3d.com/Packages/com.unity.localization@1.5/manual/Addressables.html).

La validation de cette mission utilise le mode de lecture Editor d'Addressables.
**Aucun nouveau build Windows ni parcours Player empaqueté n'est validé ici.**
Avant un futur Player, générer et vérifier le contenu Addressables pour sa cible
par **Addressables Groups → Build → New Build → Default Build Script**, ou l'API
`AddressableAssetSettings.BuildPlayerContent`, puis tester le Player sans dépendre
du cache Editor. [Construction du contenu Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/Builds.html).

Le builder `ReleaseBuild.cs` existant n'appelle pas explicitement cette API.
La configuration Addressables du projet fixe cependant
`BuildAddressablesWithPlayerBuild = PlayerBuildOption.BuildWithPlayer` : Unity
préparera le contenu avec un futur build Player, indépendamment des préférences
locales de l'éditeur. Ce réglage a été enregistré sans lancer de build. Cette
intégration ne modifie pas le builder et ne remplace pas la validation native du
contenu empaqueté sur la plateforme cible.

Tests ciblés : `GameLocalizationTests`, les méthodes `Localization*` de
`GameOptionsIntegrationTests` et les sessions concernées de
`GamePlaySessionTests`. Les fixtures doivent garder des clés et racines isolées,
attendre `PreviewReady` et l'arrêt du Bootstrap, puis restaurer l'état Editor.
Ne jamais effacer les préférences ou sauvegardes réelles pour obtenir un résultat.
Les résultats effectivement exécutés et la revue visuelle restent décrits dans
le bilan de livraison ; ils ne prouvent ni un Player futur ni le matériel physique.
