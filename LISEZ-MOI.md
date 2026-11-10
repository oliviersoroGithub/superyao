# Super Yao sous Unity : étape 1, la marche de Yao (téléphone Android et iPhone)

Ce dossier contient de quoi faire marcher Yao dans une scène d'essai du stage 1, avec des boutons tactiles.

## Ce qu'il contient
- `Assets/SuperYao/Resources/Yao/Walk/` : les 18 images de la marche, détourées, à la même taille, alignées sur les pieds.
- `Assets/SuperYao/Editor/SuperYaoSpriteImport.cs` : règle l'import des images tout seul (pivot aux pieds, 160 px par case, sans compression).
- `Assets/SuperYao/Scripts/SpriteFrameAnimator.cs` : anime un sprite image par image.
- `Assets/SuperYao/Scripts/YaoController.cs` : Yao marche avec les boutons tactiles (ou les flèches du clavier sur le PC) et se retourne.
- `Assets/SuperYao/Scripts/TouchControls.cs` : les boutons gauche et droite, dans la zone sûre de l'écran (hors encoche).
- `Assets/SuperYao/Scripts/Stage1Bootstrap.cs` : construit la scène d'essai (sol, Yao, caméra qui suit, boutons, mode paysage, 60 images/s).

## Installer
1. Installer **Unity Hub**, puis depuis le Hub la dernière version **Unity 6 LTS**, avec les modules
   **Android Build Support** (et ses outils OpenJDK et Android SDK & NDK) et **iOS Build Support**.
2. Dans Unity Hub, créer un projet avec le modèle **Universal 2D**, nommé `SuperYao`, **hors de OneDrive**
   (par exemple dans `C:\Users\olivi\Projects`). OneDrive modifie sans arrêt les fichiers et gêne Unity.
3. Fermer Unity, puis copier le dossier `Assets/SuperYao` d'ici dans le dossier `Assets` du nouveau projet.
4. Rouvrir le projet : Unity importe les images avec les bons réglages.
5. Dans **Edit > Project Settings > Player**, onglets Android et iOS : **Default Orientation = Landscape Left**.

## Essayer sur le PC
1. Créer une scène vide (File > New Scene > Basic 2D), l'enregistrer sous `Stage1`.
2. Créer un objet vide (GameObject > Create Empty) et lui ajouter le composant **Stage1Bootstrap**.
3. Ouvrir **Window > General > Device Simulator** et choisir un téléphone (un iPhone récent, un Android) pour voir
   l'écran, l'encoche et la zone sûre.
4. Appuyer sur **Play** : Yao marche avec les boutons (clic de souris) ou avec les flèches du clavier.

## Essayer sur le téléphone
- **Android** : File > Build Profiles > Android > Switch Platform, brancher le téléphone en USB (mode développeur
  activé), puis **Build And Run**.
- **iPhone** : Unity sur Windows prépare le projet Xcode, mais il faut **un Mac avec Xcode** pour l'installer sur
  l'iPhone, ou un service de compilation en ligne (Unity Build Automation, Codemagic…).
