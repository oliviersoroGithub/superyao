# Héros de Super Yao : déplacements et poses

Le héros est **Yao** (avec son sac à dos), seul personnage jouable depuis le 2026-10-28 (Lou et Moussa retirés).
Toutes ses poses viennent d'une seule planche de 7 rangées, donc du même dessin, à la même échelle (184 px debout).
Marche et course alternent vraiment les pieds (écart mesuré : deux passages pieds rapprochés par cycle). Les règles et les valeurs viennent de la version téléphone
(`src/game/player.ts`, `constants.ts`, `render/scene.ts`, `render/characters.ts`).

## Fichiers

| Fichier | Rôle |
|---|---|
| `Scripts/HeroController.cs` | marcher, courir, élan, s'accroupir, sauter (physique, sur la racine du héros) |
| `Scripts/HeroInput.cs` | commandes : boutons tactiles, clavier, manette |
| `Scripts/HeroAnimator.cs` | choix de la pose et échelle (sur l'enfant « Visuel ») |
| `Scripts/HeroAnimationSet.cs` | les poses d'un héros (un fichier `Heroes/<Héros>/<Héros>_Animations.asset`) |
| `Scripts/HeroSwitcher.cs` | essais : touches 1, 2, 3 = Yao, Lou, Moussa |
| `Scripts/TouchControls.cs` | boutons à l'écran : ◀ ▼ ▶ à gauche, « courir » et « sauter » à droite |
| `Heroes/<Héros>/Idle, Run, Jump, Fall, Crouch/` | une image par pose, découpée des planches du téléphone |
| `Prefabs/Heros/Heros_Yao, Heros_Lou, Heros_Moussa` | préfabriqués créés par le constructeur du stage |

Pour changer une animation, il faut remplacer les images d'un dossier (même nom, numérotées 00, 01…), puis
relancer **Super Yao > Construire le stage 1**.

## Commandes

| Action | Téléphone | Clavier | Manette |
|---|---|---|---|
| Marcher | ◀ ▶ | flèches, Q/D (A/D en QWERTY) | stick gauche, croix |
| Courir | bouton « courir » (double flèche), ou garder la direction (élan) | Maj ou J | X / carré, gâchette droite |
| S'accroupir | ▼ | ↓ ou S | stick ou croix vers le bas |
| Sauter | gros bouton « sauter » | Espace, ↑ ou Z (W en QWERTY) | A / croix |

On peut maintenir plusieurs boutons à la fois (par exemple courir, avancer et sauter) et glisser le pouce de ◀ à ▶.

## Règles de déplacement (valeurs du téléphone, en cases et en secondes)

| Réglage | Valeur | Téléphone |
|---|---|---|
| Marche | 6 cases/s | 1,6 px/image |
| Course | **8,5 cases/s** — le téléphone donnait 10,3, jugé trop rapide | 2,75 px/image |
| Accélération marche / course | 45 / 76,5 cases/s² | 0,2 / 0,34 |
| Prise d'élan (au-delà de la marche, même direction, au sol) | 6,3 cases/s² | 0,028 |
| Freinage au sol / en l'air | 54 / 13,5 cases/s² | 0,24 / 0,06 |
| Demi-tour | freinage × 1,5 | idem |
| Gravité | 139,5 cases/s² | 0,62 |
| Vitesse de saut | **33,4 cases/s (4 cases de haut)** — le téléphone donnait 6 cases, jugé trop haut | 10,9 |
| Bonus de saut en courant | **+ 0,31** × vitesse horizontale (environ 4,8 cases en pleine course) | 0,55 |
| Petit saut (bouton relâché tôt) | montée ramenée à 12,75 cases/s | 3,4 |
| Chute maximale | 41,25 cases/s | 11 |
| Saut après le bord / appui retenu | 7 images (0,117 s) chacun | 7 / 7 |
| Boîte de collision debout / accroupi | 0,75 × 1,375 / 0,75 × 0,875 case | 12 × 22 / 12 × 14 px |

- **Sauter sur place** : sauter sans direction. **Sauter vers l'avant** : sauter en avançant ; le héros garde son
  élan et saute plus haut s'il court.
- **S'accroupir** : au sol seulement. Le héros ne marche plus (il glisse sur son élan) mais peut se retourner ;
  il ne se relève que s'il y a la place au-dessus. Sauter depuis la position accroupie le relève.
- La physique tourne à 60 pas par seconde (`StageSettings`), comme le téléphone.

## Poses (comme le téléphone)

| Situation | Pose |
|---|---|
| Immobile au sol | repos, en boucle à 6,7 images/s |
| En mouvement au sol | **marche** jusqu'à 8,2 cases/s (mi-chemin entre marche et course), puis **course** ; la cadence suit la distance parcourue (marche : 1 cycle de 8 images pour 3 cases, environ 16 images/s ; course : 3,75 cases, environ 22 images/s). Sans dossier `Walk`, la course sert pour les deux (règle du téléphone). |
| Accroupi | pose fixe ; Yao glisse (pose de glissade) s'il s'accroupit à plus de 2 cases/s |
| Réception | Yao : pose accroupie de réception pendant 0,1 s après un saut ou une chute de plus de 0,2 s (dossier `Land`) |
| En l'air, montée rapide (> 15 cases/s) | impulsion (image 2 du saut) |
| Montée (> 3,75) | montée (image 3) |
| Sommet (> −5,6) | sommet (image 4) |
| Descente (> −26,25) | descente (Yao : image 5 ; Lou et Moussa : image 6) |
| Longue chute | bras en l'air (3 premières images de la chute) |

Échelle : le héros debout, cheveux compris, mesure 1,25 fois sa boîte de collision, soit 1,72 case. Chaque planche
a sa propre hauteur de référence (héros debout, en px), pour que toutes les poses aient la même taille à l'écran :

| Héros | Repos, saut, chute, accroupi | Marche et course |
|---|---|---|
| Yao | 171 px (planche unique 8 × 8 : toutes les poses du même dessin) | 171 px |
| Lou | 204 px (planche 6 × 6) | 393 px |
| Moussa | 190 px (planche 6 × 6) | 392 px |

Les trois héros (planches 6 × 6, même disposition) : repos = garde (4 images, 4 images/s) ; saut = préparation, impulsion (1), montée (2),
sommet (3), descente (4), réception ; chute = bras en l'air (3 images) ; accroupi = accroupi + glissade.
Poses déjà rangées pour plus tard : `Attack` (coup de poing), `Hurt` (blessé), `Death` (défaite).

## Caméra

La caméra suit la hauteur du **dernier sol touché**, pas celle du héros en l'air : un saut ne la fait pas bouger.
Elle monte quand le héros se pose sur une plateforme plus haute, ou s'il s'approche du haut de l'écran.
