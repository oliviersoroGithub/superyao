# Stage 1 — Quartier France, Grand-Bassam : parallaxe et scène

Scène : `Assets/SuperYao/Scenes/Stage01_GrandBassam.unity`, construite par le menu
**Super Yao > Construire le stage 1 (Grand-Bassam)** (script `Editor/Stage01Builder.cs`).
On peut relancer le menu à tout moment : la scène et les préfabriqués sont refaits à l'identique.
Pour modifier l'agencement de façon durable, il faut changer les listes du script, pas la scène à la main
(une retouche à la main serait écrasée par la prochaine construction).

## Unités

- 1 unité Unity = 1 case du jeu. Yao mesure environ 1,5 case ; un étage de bâtiment environ 3 cases.
- Images importées à 160 px par case, pivot en bas au centre (les objets se posent par le pied).
- Collision du sol en **y = 0** (sous les pieds de Yao). Le dessus de l'image du sol est en y = 0,2,
  pour que Yao marche au milieu du trottoir et pas sur son bord.
- Longueur du stage : rue de 16 bandes de sol de 13,575 cases (217,2 cases), pont de 36,7 cases, puis 2 bandes
  d'arrivée, soit environ **279,5 cases**.

## Convention du parallaxe

Chaque couche porte un composant `ParallaxLayer` avec un **facteur de vitesse apparente**, rapporté au sol :

| Facteur | Effet à l'écran |
|---|---|
| 0 | fixe à l'écran (ciel) |
| 0,35 | défile à 35 % de la vitesse du sol |
| 1 | défile exactement comme le sol (aucun décalage) |
| 1,1 | défile 10 % plus vite que le sol (avant-plan) |

La couche entière est décalée de `caméra.x × (1 − facteur)` ; ses objets ne bougent jamais. Il y a un seul script
par couche, et rien n'est calculé objet par objet.
Le facteur vertical est séparé : 1 pour ce qui est posé au sol (les bâtiments restent collés au sol quand la
caméra monte), 0 pour le ciel et les nuages.

**Placement** : dans une couche de facteur f, un objet placé en x local = f × X passe au centre de l'écran quand la
caméra est en X. Dans l'éditeur hors Play, les couches lointaines paraissent donc « tassées » : c'est normal,
c'est en Play que le décor prend sa place.

## Les couches (de l'arrière vers l'avant)

| # | Objet de la scène | Calque de tri | Facteur | Vertical | Contenu |
|---|---|---|---|---|---|
| 01 | `Decor/01_Ciel` | Ciel | 0 | 0 | ciel étiré pour couvrir l'écran (`FitToCamera`) |
| 02 | `Decor/02_Nuages` | Nuages | 0,05 | 0 | 6 nuages qui dérivent seuls et sont recyclés (`CloudDrift`) |
| 02b | `Decor/02b_Ciel_Vivant` | Nuages | 0,08 | 0 | de temps en temps un vol de 3 oiseaux (8 images de battement, 14 i/s, devant les nuages) et un avion lointain (long-courrier, jet ou Cessna, derrière les nuages), recyclés (`SkyTraffic`) |
| 03 | `Decor/03_Horizon_Vegetal` | Horizon | 0,15 | 1 | bande d'horizon répétée, voilée de bleu |
| 04 | `Decor/04_Batiments_Lointains` | BatimentsLointains | 0,35 | 1 | bâtiments à 55 % de leur taille, voilés de brume |
| 05a | `Decor/05a_Batiments_Cocotiers` | PlanIntermediaire | 0,65 | 1 | façades coloniales, cocotiers, enseigne |
| 05b | `Decor/05b_Poteaux_Cocotiers_Proches` | Rue | 0,9 | 1 | poteaux électriques et câbles, lampadaires, cocotiers proches |
| 06 | `06_Monde_Jouable` | Monde / Joueur | 1 | — | sol, collisions, mobilier, plantes, Yao |
| 07 | `07_Avant_Plan` | AvantPlan | 1,1 | 1 | plantes basses en bas de l'écran, assombries |

Toutes les couches utilisent le même matériau (`Sprite-Unlit-Default`), sans éclairage 2D à calculer.

## Les cinq zones

Chaque couche range ses objets dans des groupes `Z1` à `Z5`, d'après l'endroit du parcours où ils passent à l'écran.

| Zone | X du monde | Sol | Ambiance |
|---|---|---|---|
| Z1 Entrée du quartier France | 0 – 40,7 | terre | ruine à arcades, cocotiers, poteaux électriques et câbles, grand panneau |
| Z2 Rue historique | 40,7 – 100 | pavé | façades coloniales serrées, lampadaires, bancs, jarres, caisses |
| Z3 Petite place | 100 – 122,2 | pavé | aucun bâtiment au plan intermédiaire, bouquet de cocotiers, bancs, fleurs |
| Z4 Rue résidentielle | 122,2 – 190 | terre | maisons, beaucoup de végétation, ligne électrique |
| Z5 Sortie vers le pont | 190 – 216,4 | terre | le bâti s'arrête au bord de la lagune |
| Z6 Pont de la Victoire | 216,4 – 252,3 | tablier | Yao traverse la lagune sur le pont, devant la rambarde |
| Z7 Arrivée | 252,3 – 279,5 | terre (provisoire) | en attendant les images de la plage |

## Parcours jouable (`Parcours`, couche du monde)

Images reprises du téléphone, dans `Assets/SuperYao/Gameplay/` : caisses et latérite (`Blocks`), planche et ponton
(`Platforms`), pirogue avec son pêcheur (`Pirogue`, 3 images).

| Élément | Règle |
|---|---|
| Piles de caisses ou de latérite | blocs d'une case, une boîte de collision par pile |
| Planches | dessus à 2,6 à 5 cases, on saute à travers par en dessous (PlatformEffector2D) |
| Pontons | plancher au niveau de la rue (y = 0), 3,7 cases par longueur |
| Pirogues | 5 cases de long ; navette entre deux pontons (2,4 et 3 cases/s, arrêt de 1,2 s à chaque bout) ; le héros est emporté (`MovingPlatform`) |
| Eau de la lagune | devant le héros (calque AvantPlan) : il s'y enfonce ; sous y = −2 il réapparaît au dernier point de reprise (`HeroRespawn`, provisoire) |

Après le pont (u = fin du pont) : quai u → u+6 ; saut ; ponton A u+8,5 (2 longueurs) ; **pirogue 1** jusqu'au ponton B
(u+29,85, planche au-dessus) ; saut ; **îlot** u+36 → u+44 (point de reprise) ; **pirogue 2** jusqu'au ponton C (u+62) ;
saut ; **arrivée** u+67,7 → +30, avec le grand escalier final de 6 marches. Sauts : environ 2,9 cases en marchant,
4,4 en courant.

## Sol et collisions

- Bandes en mode **Tiled** : une seule image par portion, répétée sans couture (terre 0–40,7 ; pavé 40,7–122,2 ;
  terre 122,2–230,8).
- La bordure de pierres est au même niveau sur les deux sols (le pavé est remonté de 5 px).
- Les transitions entre terre et pavé sont cachées par un massif de fleurs et par un buisson posés sur la jointure.
- Il y a **une seule** boîte de collision pour tout le sol (aucune jointure où Yao pourrait accrocher), plus un
  mur à chaque bout. Yao et le sol utilisent un matériau sans frottement (`Physics/SansFrottement`).
- Le décor n'a aucune collision.

## Caméra (`CameraFollow2D`)

| Réglage | Valeur | Rôle |
|---|---|---|
| visibleRows | 20 | hauteur visible, en cases (14 au départ, puis 17 ; vue jugée trop proche) |
| bottomY | −2 | bas de l'écran : on voit la bordure et la terre sous le trottoir |
| lookAhead | 2,5 | la caméra regarde 2,5 cases devant Yao |
| smoothTime | 0,12 s | léger amortissement horizontal |
| levelMinX / levelMaxX | 0 / ≈ 279,5 | la caméra ne montre jamais au-delà du stage |
| climbThreshold | 12 | cadrage fixe : le sol reste toujours en bas de l'écran (la caméra ne monterait qu'au-dessus de 12 cases) |
| verticalSmoothTime | 0,45 s | montée lente : les sauts ordinaires ne la font pas bouger |

## Performances mobiles

- Aucun objet n'est créé pendant le jeu : les nuages sont recyclés, et les sols et l'horizon sont des sprites répétés.
- Il y a un script par couche, pas par objet.
- Un seul matériau pour tout le décor.
- Les fonds sont compressés, avec des mipmaps (sans elles, le sol, les palmiers et les poteaux scintilleraient).
- Les petits sprites (mobilier, plantes, nuages) sont dans l'atlas `Atlases/Stage1_Decor` ; les grandes images
  restent à part.
- 60 images/s demandées par `StageSettings` (30 par défaut sur mobile).

## Tester

**Sur le PC** : ouvrir la scène, régler la vue Game sur 16:9 (ou un téléphone dans Window > General > Device
Simulator), appuyer sur Play. Yao avance avec les flèches, A/D ou Q/D, ou avec les boutons à l'écran.

**Sur Android** : installer le module Android Build Support (Unity Hub > Installs > Add modules), puis
File > Build Profiles > Android > Switch Platform, brancher le téléphone en USB (mode développeur), Build And Run.
Player Settings : Default Orientation = Landscape Left.

**Sur iPhone** : module iOS Build Support ; Unity prépare un projet Xcode, qu'il faut ouvrir sur un Mac avec Xcode
(ou compiler avec un service en ligne).

À vérifier sur téléphone : la fluidité (60 images/s), la netteté des bâtiments (images d'origine de 318 à 694 px,
agrandies sur grand écran), et la place des boutons dans la zone sûre.

## Ressources manquantes ou à surveiller

- **Câbles aériens** : aucune image fournie ; ils sont tracés par des lignes (LineRenderer) entre les isolateurs
  des poteaux électriques.
- **Pont de la Victoire et lagune** (`Bridge/bridge.png`, `Water/lagoon.jpg`) : au plan intermédiaire de la zone 5.
  L'eau commence derrière le dernier bâtiment, qui cache son bord ; les piles du pont plongent d'une case dans l'eau.
  L'image de l'eau a été rendue raccordable en fondant ses bords (220 px).
- **Pont jouable** (zone 6, couche du monde) : taille réglée par la rambarde (0,75 case, la moitié de Yao), soit
  36,7 cases de long ; tablier au niveau de la rue (y = 0), donc la même boîte de collision que le sol ; ses culées
  recouvrent le bout du sol de 0,8 case de chaque côté. Une bande d'eau passe sous le tablier, entre les piles.
- **Plage** : aucune image (sable, mer, cocotiers de plage…) ; la zone 7 est en terre ocre en attendant.
- **Bâtiments** : basse résolution (318 à 694 px de haut) pour des façades de 5,5 à 8 cases ; une version plus
  grande améliorerait la netteté sur tablette.
- **L'image du ciel** contient déjà des nuages peints, qui ne bougent pas, en plus des nuages mobiles.
