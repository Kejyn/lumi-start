# Revue humaine du parcours produit

Cette liste prépare une revue humaine du parcours produit. Compléter une copie
pour chaque build ; une case vide demeure non exécutée pour ce passage. Les tests
virtuels ne remplissent pas les cases d'une revue matérielle.

Ouvrir `Assets/Game/Scenes/GameMenu.unity`, ou un nouveau build
Windows construit depuis l'état à vérifier. Utiliser un profil de test isolé pour
les écritures et suppressions ; conserver les données réelles. Les anciennes
versions ne constituent pas une validation du build courant.

- [ ] Start : seul le texte centré apparaît et fade en temps non scaled ; Espace/A ouvre le menu, Entrée/clic reste sans effet.
- [ ] Le menu est lisible aux résolutions et ratios cibles, avec échelles UI 80/100/120 %.
- [ ] New Game apparaît sans save ; Continue apparaît après une save, puis disparaît après la dernière suppression.
- [ ] Les trois slots indiquent leur état/date ; New Game ne remplace pas un slot occupé.
- [ ] Clic/Entrée/A sur un slot occupé restaure son snapshot ; slot vide : sélection seule. N/X propose New game ou Delete save exclusivement ; une suppression confirmée conserve les autres slots et le focus.
- [ ] Keyboard, souris et manette physique naviguent sans perdre le focus dans les modales et les onglets.
- [ ] Controls scrolle jusqu'à la ligne choisie ; capture clavier/souris/manette, Esc/B, timeout et Reset sont compréhensibles.
- [ ] Pause/Saves restent disponibles ; les contrôles Common sont refusés au rebinding Gameplay.
- [ ] Apply conserve les préférences/bindings ; Back confirme les changements réels. No restaure, Yes sauvegarde, une erreur garde Options ouvert.
- [ ] Gameplay sensitivity/inversion, Graphics, Audio, Interface scale et hints ont l'effet attendu.
- [ ] Credits défile et Back retourne au menu ; Exit/No restaure le focus, Exit/Yes quitte sans erreur.
- [ ] Écoute humaine : volumes, musique, SFX et transitions sonores sans défaut audible.
- [ ] Fenêtre normale/maximisée, focus perdu/repris et périphérique branché/débranché restent utilisables.

Avant tag final : remplacer les textes/sons de démonstration selon le jeu, confirmer
les chemins/identités de données et réaliser la revue humaine du Player cible.
Une validation Editor/Mono ne vaut pas validation IL2CPP ou d'une autre plateforme.
