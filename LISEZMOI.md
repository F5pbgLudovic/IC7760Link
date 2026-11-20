# IC-7760 Link 3.0

Télécommande complète de l'ICOM IC-7760 par la liaison USB (protocole CI-V), avec cascade (waterfall) haute qualité.
Logiciel **portable et autonome** pour Windows 10 / 11 : un seul `IC7760Link.exe`, aucune installation, aucun pilote supplémentaire
(en plus du pilote USB d'Icom pour le poste). Les réglages sont mémorisés dans le dossier `data` à côté de l'exe.

## Démarrage
1. Extraire le zip dans un dossier (pas dans Program Files), lancer `IC7760Link.exe`.
2. **Sans poste** : choisir « Poste virtuel (simulateur IC-7760) » puis CONNECTER. Un IC-7760 simulé répond comme le vrai
   (deux récepteurs, spectre vivant : SSB, CW, FT8, RTTY, radiodiffusion AM, QRM).
3. **Avec le poste** : brancher le câble USB, choisir le port COM du poste (115200 bauds, adresse CI-V 94, ou B2 dans le menu déroulant), CONNECTER.
   Si la liaison est muette, le bouton ⚙ (Options) > **Monitoring et détection CI-V** ouvre le journal des trames et recherche l'adresse et la vitesse du poste.
   Le logiciel ne pilote jamais DTR/RTS (pas d'émission accidentelle par la ligne série).

## Ce que fait le logiciel
- **Cascade et spectre** : image calculée à la résolution de l'écran (interpolation cubique), défilement lissé dans le temps,
  8 palettes, niveau automatique (plancher/plafond suivent le bruit) ou réglage manuel, moyennage, maintien des crêtes,
  plan de bandes (CW / numérique / phonie / balises), étiquettes de fréquence sur les signaux, échelle de temps,
  bande passante du filtre affichée, curseur RIT et fréquence TX en split. L'historique de la cascade est recalé (pas effacé)
  quand on change la fréquence centrale ou le span.
- **Scope** : MAIN, SUB, double scope (haut/bas ou côte à côte), modes Centre / Fixe / Scroll-C / Scroll-F, span ±2,5 à ±500 kHz,
  niveau de référence, vitesse de balayage, Hold.
- **Accord** : clic ou glisser sur la cascade (Maj = pas de 10 Hz), molette (Maj = 1 kHz), Ctrl + molette = zoom,
  clic sur une étiquette de signal, chiffres de l'afficheur cliquables, double-clic pour saisir une fréquence, flèches du clavier.
- **Poste** : MAIN/SUB (fréquence, mode, filtre FIL1-3, DATA1-3), échange / égalisation, Dualwatch, Split, RIT/XIT, tuner,
  largeur de filtre IF, PBT1/2, AF/RF/SQL, PRE/ATT/AGC, NB/NR/ANF/notch (+ largeur)/APF/TPF/DIGI-SEL/IP+, forme du filtre,
  puissance, micro, compresseur, drive, moniteur, VOX, BK-IN, CW (vitesse, tonalité, délai, texte libre, 4 messages F1-F4,
  Échap = stop), mémoires vocales T1-T8, bandes avec mémoire par bande, mémoires (appel, écriture, M→V, effacement), scan.
- **Bargraphes** : S (MAIN et SUB), Po, SWR, ALC, COMP, Vd, Id.
- **Options (⚙)** : couleurs du waterfall et de l'interface, **Réglages avancés du poste** (toutes les commandes du catalogue CI-V, 530 entrées, dont tout le menu SET, en lecture/écriture), **Monitoring et détection CI-V**. Bouton ⇅ : export / import de la configuration.
- **Mode d'emploi complet** : voir `AIDE.md` (français) et `AIDE-EN.md` (English).

## À savoir (important)
- Cette version a été développée contre le **poste virtuel** (autotest : `IC7760Link.exe --selftest=rapport.txt`). Premiers essais sur un IC-7760 réel : connexion et cascade confirmées ; le reste (écriture des réglages, émission) est à valider : testez l'émission sur charge fictive.
- Les commandes viennent du catalogue CI-V extrait du guide A7788-8EX-2 (mai 2025). Ce catalogue ne contient pas le détail des
  pages 24-26 du guide (format exact des trames du scope, index des largeurs de filtre). Ces formats ont été **supposés identiques
  à ceux de la famille IC-7300 / IC-7610** : trames `27 00` en divisions de 50 points, en-tête (mode, fréquences, hors-plage),
  amplitude 0-160, largeur de filtre `1A 03` indexée 0-40 (50 Hz à 3,6 kHz). Le décodage tolère un nombre de points différent.
  Si l'affichage réel diffère, le fichier de trames enregistré par le moniteur CI-V suffit pour corriger.
- Les réponses aux commandes « directes » `29` (MAIN/SUB) sont acceptées avec ou sans le préfixe `29`.

## Compiler
`powershell -ExecutionPolicy Bypass -File build.ps1` (utilise le compilateur .NET Framework fourni avec Windows).
Le catalogue `doc\ic7760.civ` est intégré à l'exe.

Licence : GPL v3. Projet indépendant, non affilié à ICOM.

## Nouveautés 1.1
- La fenêtre garde ses proportions et se redimensionne d'un bloc (boutons, polices, cascade) : rien n'est tronqué, aucun ascenseur.
  En plein écran, des bandes noires comblent la différence de proportions.
- `Diagnostic.bat` (ou `IC7760Link.exe --diag=COM5,115200,94`) enregistre 12 s de trames CI-V dans `diagnostic-ci-v.txt`.
- `AIDE-POUR-L-AMI.txt` : mode d'emploi du premier essai sur le vrai poste et liste des fichiers à renvoyer.

## Nouveautés 1.4
- Clic droit sur les vumètres : bascule entre les barres et un vumètre à aiguille façon IC-7760 (S en réception, Po en émission, SWR en bleu, ALC/COMP/Vd/Id en chiffres). Le choix est mémorisé.
- Adresse CI-V adoptée automatiquement si le poste répond avec une autre adresse (ex. 94).

## Nouveautés 1.5
- Vumètres à aiguille au format 4/3 façon écran IC-7760 (MAIN et SUB côte à côte : échelles S, Id, Po, SWR, COMP, ALC, Vd, BW, mode, filtre). Clic droit = barres/aiguilles ; clic gauche = changer MET (Po, SWR, ALC, COMP, Vd, Id) pour l aiguille en émission.

## Nouveautés 1.6
- Les deux vumètres à aiguille occupent toute la largeur (plus de bandes noires) ; aiguilles plus épaisses.

## Nouveautés 1.7
- Cascade et spectre plus fluides : défilement lissé entre deux trames du poste, courbe adoucie (menu clic droit : Défilement fluide).
- Échelle de fréquence plus précise : graduations au 1/10, repère médian, grille dans la cascade.
- Bouton carré à côté du ? : fenêtre d export / import de toute la configuration (fichier .ini, copier, appliquer ; le logiciel redémarre après un import).

## Nouveautés 1.8
- Export / import (bouton carré ⇅) : sauvegarde aussi tous les réglages du poste affichés sur les potentiomètres et boutons (fréquences, modes, filtres, niveaux, NB/NR, scope...) quand le poste est connecté. Import : le logiciel redémarre et renvoie les réglages au poste dès la connexion ; le bouton "Envoyer au poste" les renvoie tout de suite. Aucune commande d accord ou d émission n est rejouée.

## Nouveautés 1.9
- Bouton BANDE / FIXE à droite de EFFACER (barre du scope). FIXE : le curseur reste au centre, la cascade défile. BANDE : la cascade est fixe tant que la fréquence est dans une bande radioamateur (160 m à 6 m) ; hors bande elle suit la fréquence (curseur au centre) ; de retour en bande elle redevient fixe. Le choix est mémorisé. Les bornes de la cascade fixe sont celles réglées dans le poste (Fixed Edges).

## Nouveautés 2.0
- FIXE : le logiciel force le mode CENTRE du poste (curseur toujours au milieu). BANDE : hors bande radioamateur, la cascade passe en mode centre en conservant la même échelle (largeur) que la cascade fixe.
- Export / import simplifié : petite fenêtre Exporter / Importer / Annuler (le contenu est écrit dans le fichier).
- Nouveau bouton carré Options (engrenage, au-dessus du bouton export/import) : menu Couleurs du waterfall - dégradé de 10 couleurs réglables (palette 64 couleurs + RVB + hexa), couleurs des curseurs, de la courbe, des crêtes et de la bande passante. La palette "Personnalisée" apparaît aussi dans la liste des palettes.

## Nouveautés 2.1
- En mode FIXE (curseur au centre), le curseur et la bande passante restent strictement au milieu de la cascade, sans à-coup quand on change la fréquence.
- Options > Couleurs de l interface : arrière-plan, panneaux, boutons, bordures, texte, accents, rouge/vert/bleu/cyan, chiffres de fréquence MAIN et SUB, fond des afficheurs. Changement immédiat, mémorisé, inclus dans l export.

## Nouveautés 2.2
- Heure UTC (HH:mm:ss) sur le côté gauche de la cascade, qui descend avec elle, un repère toutes les 4 lignes de texte (remplace les repères -5 s).
- VITESSE de x0,1 à x5 : clic gauche augmente, clic droit diminue, retour au début en bout de course.
- MOY de 1 à 20 : clic droit augmente, clic gauche diminue, retour en bout de course.
- PICS : clic droit = durée de maintien des crêtes (normal, ou 1 à 10 s en plus, 3 s par défaut).
- Bouton FIXE/BANDE supprimé : les boutons CENTRE et FIXE du scope gardent leur fonctionnement.

## Nouveautés 2.3
- PICS actif : les fréquences affichées sur le spectre se placent sur les crêtes maintenues et restent donc affichées plus longtemps.
- Moniteur CI-V : affiche l adresse CI-V détectée sur la liaison (ou signale que le logiciel écrit à une autre adresse que celle du poste) ; bouton Détecter le poste : essaie 7 vitesses sur le port COM choisi (poste déconnecté), lit l identifiant du poste et propose de reporter adresse et vitesse.

## Nouveautés 2.4
- Les boutons SET et CI-V sont déplacés dans Options (engrenage) : menus "Réglages avancés du poste" (ancien SET) et "Monitoring et détection CI-V" (ancien CI-V). Le bouton ÉMISSION (PTT) occupe la place libérée.

## Nouveautés 2.5
- Adresse CI-V par défaut 94h (liste déroulante 94 / B2).

## Nouveautés 2.6
- Documentation complète (README, AIDE.md, CHANGELOG), licence GPL v3, publication sur GitHub : https://github.com/F5pbgLudovic/IC7760Link

## Nouveautés 2.8 (dont 2.7)
- Clic droit sur un bouton ou un potentiomètre = verrouillage (rouge, mémorisé). Un potentiomètre verrouillé bloque ses touches associées (largeur de filtre, puissance, RIT 0, AUTO). Couleur réglable dans Options.
- Puissance : touche 200 W (ex-MAX) et P.MAX (limite le potentiomètre et grise les préréglages supérieurs).
- Zone « Votre indicatif » clarifiée (remplace {C} dans les messages F1-F4).
- Options > Réglage Molette souris : vitesse des potentiomètres à la molette, 1 à 10 % par cran (2 % par défaut). Boutons STOP non verrouillables.
- Vumètre à aiguille : puissance moyennée avec repère de crête en émission ; chiffres de fréquence sans « 8 » ni zéros de tête grisés ; titre « IC-7760 Link V2.8 @F5PBG 2026 ».

## Nouveautés 3.0
- **Bouton TONE** (tonalité 1500 Hz sur la carte son USB du poste + PTT ou VOX), **micro du PC vers le poste**, **alerte ROS** (bips, fenêtre rouge, chute de puissance), **étalonnage** des vumètres Po et ROS, maintien du maximum réglable, **mise à jour en un clic** depuis GitHub : fenêtre **Outils** (bouton ⚑), voir AIDE.md § 6 bis.

## Nouveautés 2.9
- Export enrichi : application + réglages affichés du poste + **menu interne de l'IC-7760 (SET)** (232 entrées numériques) + **mémoires de fréquences du poste** (canaux 1-99, bords de scan). Import par cases à cocher (application / réglages du poste / menu / mémoires ; mémoires décochées par défaut car elles écrasent le poste).
- Aide en français (AIDE.md) et en anglais (AIDE-EN.md). Correction d'un plantage à la réouverture des Options.
