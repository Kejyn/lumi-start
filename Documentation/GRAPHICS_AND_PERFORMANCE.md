# Graphics et performances

Unity 6000.3.13f1, URP 17.3.0. Les réglages utilisent le brouillon Options et le
document Settings V1 existants. Apply applique et sauvegarde ; Reset prépare les
défauts ; Back conserve la confirmation avant abandon. Les anciennes préférences
gardent résolution, profil Unity, volumes et langue ; les nouveaux champs absents
reprennent les défauts du jeu. Aucune migration des sauvegardes de partie.

## Choix et effets réels

| Choix | Application |
| --- | --- |
| General Quality | Low, Medium, High ; Custom est calculé après modification individuelle. |
| Resolution / Display | Choix existants fournis par le backend natif. |
| FPS Limit | 30, 60, 120 ou Unlimited (-1), via Application.targetFrameRate. |
| VSync | vSyncCount 1/0. Sur desktop, actif : targetFrameRate=-1, limite préférée conservée mais désactivée dans la vue. |
| Render Scale | 50–100 %, par pas de 5 %, sur le clone URP. |
| Anti-aliasing | Off, FXAA, SMAA ; MSAA 2x/4x/8x si renderer et format matériel compatibles. |
| Post-processing | renderPostProcessing sur les caméras Game URP ; n'ajoute aucun effet. |
| Shadow Quality | Off : distance effective 0 ; Low/Medium/High : résolution et cascades ci-dessous. |
| Shadow Distance | 5–100 m par pas de 5 dans la vue ; le contrat Core accepte 0–200 m. Désactivé avec Shadows Off. |
| Texture Quality | Limite des mipmaps globaux 2/1/0 pour Low/Medium/High. |
| Anisotropic Filtering | Off, Per Texture, Forced On selon l'enum Unity. |
| LOD Quality | lodBias 0,5 / 1 / 2. |

Les options non exposées par le backend sont masquées. Les limites texture ne
modifient que les textures compatibles avec les mipmaps ; le LOD ne devient
visuellement différent qu'avec des LODGroups. Les préférences n'inventent pas de
LOD pour le cube. Le filtrage anisotrope dépend aussi des réglages d'import.

FXAA/SMAA utilisent UniversalAdditionalCameraData.antialiasing ; MSAA utilise
l'asset URP et Camera.allowMSAA. Les premières nécessitent le post-processing :
le couper sélectionne Off et les retire des choix disponibles. Les caméras des
deux scènes possèdent leur composant URP ; les caméras ajoutées par un futur jeu
doivent aussi le posséder pour participer à ces réglages. Le backend ne leur
ajoute pas de composants à l'exécution.

Un changement futur de pipeline ou de renderer doit réévaluer les capacités
exposées et adapter ce backend avant de proposer de nouvelles options.

TAA n'est pas proposé : son historique temporel et ses contraintes de motion
vectors, stacking et MSAA nécessitent une intégration dédiée. Les modes MSAA
sont filtrés pour tous les renderer data déclarés et les échantillons réellement
pris en charge. [Référence AA URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/anti-aliasing.html).

## Presets du Starter

| Paramètre | Low | Medium (défaut) | High |
| --- | --- | --- | --- |
| Render Scale | 75 % | 100 % | 100 % |
| AA | Off | FXAA | SMAA |
| Post-processing | Off | On | On |
| Ombres | 512 / 1 cascade / 20 m | 1024 / 2 cascades / 35 m | 2048 / 4 cascades / 50 m |
| Textures | Low | High | High |
| Anisotropic | Off | Per Texture | Forced On |
| LOD | Low | Medium | High |

Le preset préserve résolution, affichage, FPS/VSync, profil Unity, audio et langue.
Les presets ne sont pas des profils Unity fictifs : QualityName reste PC ou Mobile.
Si des capacités réduites rendent deux presets identiques, le premier correspondant
détermine le libellé. Modifier GameGraphicsPresets pour les besoins d'un autre jeu.
Les paramètres moteur capturés sont restaurés à l'arrêt ; les préférences
enregistrées restent persistées.

## Assets, cycle de vie et coût

GameBootstrap injecte GameGraphicsBackend dans l'unique SettingsService. Core ne
référence ni URP ni Windows ; son interface optionnelle IRenderingSettingsBackend
préserve les anciens backends. Le projet référence les assemblies URP déjà installées.

PC_RPAsset et Mobile_RPAsset restent les templates. Le backend possède un clone
runtime par qualité touchée, sans écrire les assets. Il conserve HDR, additional
lights, renderer features et leurs ressources Depth/Opaque du jeu ; aucune passe
SSAO, palette, tonemapping ou shader n'a été remplacée. Le profil PC utilise
Forward+ et SSAO : désactiver ses ressources sans examiner ses consommateurs
pourrait modifier le rendu. Le clone Medium réduit surtout ombres/cascades par
rapport au template PC (2048, 4 cascades, 50 m).

Release restaure les références aux pipelines, paramètres QualitySettings,
cadence, affichage et états caméra capturés ; il retire les abonnements et détruit
les clones après restauration. Les nouvelles scènes reçoivent les réglages sur
leurs caméras Game. Attendre l'arrêt Bootstrap avant une sortie contrôlée.

- GPU : résolution interne, AA, ombres, additional lights, HDR et post-process.
- CPU : culling, soumission, LOD et travail des lumières/ombres ; Forward+ n'utilise
  pas la limite de lumières supplémentaires par objet comme le rendu Forward.
- Mémoire/bande passante : mipmaps, cibles HDR, MSAA, Depth/Opaque, shadowmaps.

Un coût théorique réduit ne prouve pas un gain FPS. Mesurer CPU/GPU/mémoire sur un
build représentatif avant de retirer HDR, SSAO, Depth/Opaque ou d'ajuster les lights.
Le Starter ne garantit aucune cadence ou budget matériel.

## Windows et consoles

Contrôle natif de cette version : Editor Direct3D12 ; Windows Player **Auto
Graphics API=true**, liste native **Direct3D12, Direct3D11**, aucun Device Filtering
Asset personnalisé. Cette configuration est conservée : DX11 reste inclus.

Auto utilise la liste intégrée Unity. Le filtrage D3D12 intégré peut écarter des
appareils et utiliser l'API de repli configurée ; ce mécanisme ne garantit pas de
récupérer toute erreur d'initialisation ou tout crash de driver DX12. Une préférence
explicite ne se justifierait qu'après mesure ou problème matériel reproductible.
[Configuration des APIs](https://docs.unity3d.com/6000.3/Documentation/Manual/configure-graphicsAPIs.html),
[filtrage D3D12](https://docs.unity.com/en-us/engine/6000.3/manual/introduction-d3d12-device-filtering-asset).

Aucune fonctionnalité actuelle du Starter identifiée n'impose DX12. URP annonce
DX11 feature level 11.0 minimum ; vérifier aussi OS, pilotes et exigences Unity
du matériel cible. Aucun DX9, sélecteur DX10, Vulkan, OpenGL ou choix d'API en jeu
n'est ajouté. [Exigences URP](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/requirements.html).

Consoles : la séparation du backend permet une adaptation future, sans SDK ni
validation constructeur. Ne pas transposer les règles DirectX Windows à leurs
APIs. Les limites FPS/VSync sont exposées pour les plateformes effectivement
reconnues ; VSync n'est pas proposé au navigateur ou sur mobile. Les cibles console
nécessitent leur propre backend et leurs capacités validées.

## Ajouter un style PSX dans un futur jeu

La réduction de coût et le style sont deux décisions séparées. Un jeu pourra
ajouter une faible résolution fixe/pixelisation, textures Point, dithering,
vertex snapping ou renderer features spécifiques dans son propre rendu. Garder
ces effets et leur configuration artistique hors du Core ; vérifier shaders,
UI et upscale sur chaque API moderne ciblée. Aucun effet rétro n'est implémenté
ici et le style PSX ne nécessite pas DirectX 9.

## Validation humaine à compléter

Tester le parcours Graphics dans GameMenu puis dans la pause GameDemo : défilement,
lisibilité aux ratios/échelles cibles, clavier/souris et manette Xbox physique,
Apply/Reset/Back, plein écran et changement de résolution réels. Sur un futur build,
contrôler DX11/DX12 séparément, chaque mode AA, focus perdu/repris et budgets de
performance sur le matériel minimum. Aucun build ou benchmark prolongé n'a été
effectué pendant ce développement.
