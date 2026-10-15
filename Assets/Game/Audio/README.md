# Sons temporaires du Starter

Les quatre WAV ont été synthétisés localement, sans téléchargement,
échantillon tiers, enregistrement vocal ou mélodie empruntée. Format PCM mono,
16 bits, 24 kHz. Ils constituent des signaux temporaires de validation.
Les fichiers existants peuvent être importés et utilisés sans outil de génération.

| Fichier | Durée | Génération / usage |
| --- | --- | --- |
| DemoMusic.wav | 4 s | Sinusoïde 220 Hz, multiplicateurs harmoniques et enveloppes ; boucle de menu/jeu. |
| UiClick.wav | 0,1 s | Sinusoïde 880 Hz avec attaque et décroissance ; boutons UI. |
| Gameplay2D.wav | 0,32 s | Sinusoïde 440 Hz avec enveloppe ; F / bouton ouest. |
| Gameplay3D.wav | 0,45 s | Sinusoïde 330 Hz avec enveloppe ; G / bouton nord, position du cube. |

Ces sons originaux générés pour Lumivale Studio peuvent être utilisés, modifiés et
redistribués avec ces projets et leurs dérivés, sans attribution obligatoire.
Aucune licence d'asset tiers n'intervient. Les sons et le mixer appartiennent au
Starter ; aucun de ces assets n'est inclus dans le package réutilisable.

L'importeur utilise DecompressOnLoad, PCM, préchargement des données, sans chargement
en arrière-plan. Le mixer `LumiDemo.mixer` possède Master avec Music et SFX comme
enfants, et expose `MasterVolume`, `MusicVolume`, `SfxVolume`.

Conserver les fichiers et leurs `.meta` pour préserver les références du jeu.
Le runtime utilise l'API publique Unity Audio ; aucun outil historique n'est requis.
