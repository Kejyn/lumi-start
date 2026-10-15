# Validation humaine du Player Windows

Compléter une copie de cette checklist pour chaque build à valider.
Les événements virtuels et l'état `AudioSource.isPlaying`
ne constituent pas une validation des périphériques physiques ou de l'écoute.

## Conditions du passage

Renseigner avant de commencer :

- Testeur/date : ____________________
- Commit Starter / Core et variante du build : ____________________
- Windows/GPU/écran et résolution native : ____________________
- Clavier/souris/manette (modèles, USB/Bluetooth) : ____________________
- Sortie audio (casque/enceintes) : ____________________
- Chemin du Player, journal et données de validation : ____________________

Utiliser **le build Validation isolé**, pas les données du Player normal. Confirmer
le Company/Product et les chemins de stockage avant de modifier un slot. Préserver
toute donnée antérieure de cette identité de validation. Ne supprimer ni préférences
ni sauvegardes utilisateur du Starter. Ne pas exécuter de PlayerPrefs.DeleteAll.

Chaque ligne attend un résultat humain daté : **réussi**, **échoué** ou **non
exécuté**, avec commentaire/preuve. Une case vide garde le statut non exécuté.

## Parcours et périphériques

| Vérification réelle | Résultat / commentaire |
| --- | --- |
| Démarrage du menu : textes, police/glyphes et boutons visibles, aucun blocage | Non exécuté |
| Clavier physique : flèches/WASD, Entrée/Espace, Play et navigation des champs | Non exécuté |
| Gameplay : déplacement physique WASD/flèches et compteur C | Non exécuté |
| Échap : Pause, Resume, retour menu annulé puis confirmé | Non exécuté |
| Souris physique : clics, sliders, choix, UI après changement de scène sans déplacer le pointeur | Non exécuté |
| Gameplay : bouton Save/Load et clic droit avec souris réelle | Non exécuté |
| Manette physique : stick/croix, bouton sud/est, Start Pause et reprise | Non exécuté |
| Manette physique : LB Saves, RB compteur, boutons ouest/nord SFX | Non exécuté |
| Menu → Gameplay → Pause → Menu, trois fois : navigation/sélection correctes, aucun doublon audible/visible | Non exécuté |
| Options/Saves au-dessus de Pause : vue supérieure seule interactive, Back conserve Pause | Non exécuté |
| Pause : cube arrêté ; Options/Saves/fondus audio restent utilisables à timeScale zéro | Non exécuté |

## Graphiques et préférences

| Vérification réelle | Résultat / commentaire |
| --- | --- |
| Options : modifier le brouillon puis Back ; aucune application/sauvegarde involontaire | Non exécuté |
| Reset Defaults : brouillon remis aux défauts, puis Apply & Save | Non exécuté |
| Modes réellement proposés : Windowed, FullScreenWindow et ExclusiveFullScreen lorsqu'ils sont disponibles | Non exécuté |
| Deux résolutions réellement disponibles : taille/mode constatés, UI lisible, pointeur aligné | Non exécuté |
| Changement de qualité disponible : choix gardé, aucun profil retiré | Non exécuté |
| Alt-Tab puis retour et redimensionnement fenêtre : navigation et affichage utilisables | Non exécuté |
| Après fermeture complète et relancement : valeurs appliquées/sauvegardées retrouvées | Non exécuté |

## Écoute réelle

| Vérification réelle | Résultat / commentaire |
| --- | --- |
| Musique audible sur la sortie indiquée, continuité Menu/Gameplay, aucun doublon | Non exécuté |
| SFX UI audible lors des interactions attendues | Non exécuté |
| SFX 2D F/ouest et 3D G/nord audibles, comportement spatial cohérent | Non exécuté |
| Master/Music/SFX : chaque slider affecte la catégorie attendue ; zéro silencieux et retour audible | Non exécuté |
| Modification des volumes en pause et fondus : aucun blocage ni saut inattendu | Non exécuté |
| Fermeture du Player : musique terminée, aucun son résiduel du processus | Non exécuté |

## Sauvegardes et processus

Utiliser uniquement les slots de l'identité de validation. Relever compteur et
position avant/après pour comparer ; sauvegarder les fichiers de cette identité
avant toute manipulation de corruption. La récupération peut être vérifiée avec
deux générations Save du même slot, Player fermé, puis copie/préservation du
principal avant de le corrompre intentionnellement. Le backup reste intact.

| Vérification réelle | Résultat / commentaire |
| --- | --- |
| Slots demo-1/demo-2/demo-3 séparés : Save, mutation du cube/compteur, Load restaure le bon snapshot | Non exécuté |
| Load d'un slot absent : diagnostic lisible, gameplay inchangé | Non exécuté |
| Delete : Cancel conserve le slot, Confirm le supprime sans toucher les autres | Non exécuté |
| Principal corrompu de validation + backup : récupération signalée, données précédentes restaurées | Non exécuté |
| Après récupération : Save refuse le principal corrompu ; réparation décidée explicitement via Delete confirmé | Non exécuté |
| Gameplay → Menu → Gameplay : même contenu sauvegardé, puis Load correct | Non exécuté |
| Fermeture complète du processus, relancement et Load : sauvegarde retrouvée et restaurée | Non exécuté |
| Préférences et slots du Player normal/utilisateur inchangés pendant ce passage | Non exécuté |
| Journal réel relu : aucune exception/erreur bloquante ; warnings expliqués | Non exécuté |

## Décision du studio

Anomalies et validations encore manquantes : ____________________

Rapport/journaux/captures ajoutés à la revue : ____________________

Validation explicite du rapport et des essais manuels requis : ____________________

Le tag **`v0.1.0`** est une proposition. Sa création définitive et la déclaration de
V0.1 officiellement stable attendent la validation de Lumivale Studio.
