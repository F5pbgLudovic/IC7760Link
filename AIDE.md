# IC-7760 Link — Aide / mode d'emploi

Ce guide explique comment utiliser **IC-7760 Link**, la télécommande CI-V et cascade (waterfall) pour l'ICOM IC-7760.
**English version: [AIDE-EN.md](AIDE-EN.md).** Pour une présentation rapide, voir le [README](README.md) ; pour le premier essai sur un vrai poste, voir [dist/AIDE-POUR-L-AMI.txt](dist/AIDE-POUR-L-AMI.txt).

## 1. Installer et lancer

1. Téléchargez `IC7760Link-X.Y.zip` depuis la page [Releases](https://github.com/F5pbgLudovic/IC7760Link/releases/latest).
2. **Extrayez** le zip dans un dossier de votre choix (Bureau, clé USB…). N'utilisez pas le dossier Program Files et ne lancez rien depuis l'intérieur du zip.
3. Lancez `IC7760Link.exe`. Aucune installation. Les réglages sont mémorisés dans le sous-dossier `data` créé à côté de l'exe.

Windows peut afficher un avertissement SmartScreen au premier lancement (exécutable non signé) : « Informations complémentaires » puis « Exécuter quand même ».

## 2. Se connecter

### Essayer sans poste
Dans la liste en haut à droite, choisissez **Poste virtuel (simulateur IC-7760)** puis cliquez **CONNECTER**. Un IC-7760 simulé répond comme le vrai (deux récepteurs, spectre vivant avec SSB, CW, FT8, RTTY, radiodiffusion et QRM).

### Avec le poste
1. Allumez l'IC-7760 et branchez le câble USB (pilote USB d'Icom installé).
2. Dans le poste (menu SET > Connectors > CI-V) : vitesse **115200** ou « Auto », « CI-V USB Echo Back » = OFF.
3. Dans le logiciel : choisissez le port COM du poste (voir le Gestionnaire de périphériques, section « Ports »), vitesse **115200**, adresse CI-V **94** (ou **B2**, selon le réglage du poste), puis **CONNECTER**.
4. Fermez les autres logiciels qui utilisent le même port (HRD, WSJT-X, logiciel d'Icom…).

Le logiciel n'agit **jamais** sur DTR/RTS : aucune émission accidentelle par la ligne série.

> **Adresse CI-V** : si le poste répond avec une autre adresse que celle réglée, le logiciel adopte automatiquement celle du poste.
> Si rien ne se connecte, voir « Dépannage » plus bas.

## 3. L'écran

| Zone | Contenu |
|---|---|
| Haut gauche | Afficheurs de fréquence **MAIN** et **SUB**. Cliquez sur un chiffre pour l'incrémenter, double-clic pour saisir une fréquence. |
| Haut centre | Deux **vumètres** (S, Po, SWR, ALC, COMP, Vd, Id), en barres ou à aiguille. |
| Haut droite | Liaison (port COM, vitesse, adresse CI-V, CONNECTER), horloge UTC, ÉMISSION (PTT), boutons ?, ⇅ (export/import) et ⚙ (Options). |
| Barres de boutons | Modes (LSB, USB, CW, RTTY, AM, FM, PSK…), filtres FIL1-3, DATA1-3, M=S / M↔S, Dualwatch, Split, RIT/XIT, Tuner, pas. Puis la barre du **scope**. |
| Centre | **Spectre** et **cascade** (waterfall) de MAIN, SUB ou les deux. |
| Bas | Console : bandes et mémoires, récepteur (AF, RF, SQL, NR, NB, notch, PRE, ATT, AGC…), filtre (largeur, PBT, forme), émission (puissance, micro, compresseur, drive, moniteur, VOX, BK-IN), CW et mémoires vocales. |

La fenêtre se redimensionne librement en **gardant ses proportions** : tout reste visible et il n'y a jamais d'ascenseur. En plein écran, des bandes noires comblent la différence de proportions.

## 4. Accorder le poste

- **Clic** sur la cascade ou le spectre : accorde la fréquence (Maj = pas de 10 Hz). Le glisser fonctionne aussi.
- **Molette** : fréquence par pas (réglable avec « Pas » ; Maj = 1 kHz).
- **Ctrl + molette** : zoom (largeur du scope).
- **Clic sur une étiquette de fréquence** au-dessus d'un signal : accorde sur ce signal.
- **Flèches du clavier**, chiffres cliquables, saisie directe (double-clic sur l'afficheur).
- **Échap** : arrête une émission CW en cours.

## 5. Le scope (spectre et cascade)

### Boutons de la barre du scope
- **SCOPE MAIN / SCOPE SUB / DUAL ↕ / DUAL ↔** : quel récepteur est affiché et comment (un seul, ou deux scopes l'un au-dessus de l'autre ou côte à côte).
- **CENTRE / FIXE / SCROLL-C / SCROLL-F** : modes du scope du poste.
  - **CENTRE** : la fréquence reste au centre, la cascade défile quand on change de fréquence. Le curseur reste strictement au milieu.
  - **FIXE** : la fenêtre de fréquence est fixe (bornes « Fixed Edges » réglées dans le poste), le curseur se déplace.
  - **SCROLL-C / SCROLL-F** : modes défilants du poste.
- **− / ±2,5 … ±500 / +** : largeur du scope (demi-largeur en kHz). « + » zoome, « − » élargit.
- **RÉF − / RÉF +** : niveau de référence du scope. **BAL. RAPIDE/MOYEN/LENT** : vitesse de balayage du poste. **HOLD** : fige l'affichage.
- **Palette** (liste « Cascade ») : IC-7760, Thermique, Océan, Viridis, Ambre, Matrix, Arc-en-ciel, Gris, **Personnalisée**.
- **AUTO** : niveau automatique (plancher et plafond suivent le bruit). Les deux petits potentiomètres règlent le bas et le haut quand AUTO est coupé.

### Réglages de la cascade
- **VITESSE** (x0,1 à x5) : clic gauche = plus vite, clic droit = plus lent ; en bout de course, on repart de l'autre côté.
- **MOY** (1 à 20) : moyennage. Clic droit = augmente, clic gauche = diminue.
- **PICS** : clic gauche = active/désactive le maintien des crêtes (ligne des maxima) et les fréquences affichées sur les crêtes. Clic droit = durée de maintien : normal, ou 1 à 10 s en plus (3 s par défaut).
- **PLAN** : plan de bandes (CW / numérique / phonie / balises) sous le spectre.
- **EFFACER** : efface l'historique de la cascade.
- **Menu clic droit** sur le scope : niveau automatique, **défilement fluide**, maintien des crêtes, plan de bandes, fréquences des signaux, remplissage du spectre, moyennage, enregistrement d'une image…

### Lecture de l'écran
- **Heure UTC** (HH:mm:ss) sur le côté gauche de la cascade : un repère toutes les 4 lignes de texte, qui descend avec l'image.
- **Échelle de fréquence** : graduations au 1/10 de l'intervalle principal, grille discrète dans la cascade.
- Le **curseur** rouge (MAIN) ou bleu (SUB), la **bande passante** du filtre, le curseur RIT et la fréquence d'émission en split sont affichés.
- Quand vous changez de fréquence ou de largeur, l'historique de la cascade est **recalé** (il n'est pas effacé).

## 6. Les vumètres

- Barres : S (MAIN et SUB), Po, SWR, ALC, COMP, Vd, Id.
- **Clic droit** sur la zone des vumètres : bascule entre les **barres** et les deux **vumètres à aiguille** façon écran de l'IC-7760.
- Sur les vumètres à aiguille : l'aiguille indique le **S** en réception et la mesure choisie en émission ; **clic gauche** = change la mesure (MET : Po, SWR, ALC, COMP, Vd, Id). Le mode, le filtre et la largeur BW sont rappelés.

## 7. Options (bouton ⚙)

La fenêtre Options a un menu à gauche :

1. **Couleurs du waterfall** — le dégradé de la cascade en 10 couleurs (du fond au signal le plus fort), « Partir d'une palette existante », et les couleurs du curseur MAIN, du curseur SUB, de la courbe du spectre, des crêtes et de la bande passante. Pour chaque couleur : palette de 64 couleurs, curseurs R/V/B, code hexadécimal.
2. **Couleurs de l'interface** — arrière-plan, panneaux, boutons, bordures, texte, texte atténué, accent, cyan, rouge, vert, bleu, chiffres de fréquence MAIN et SUB, fond des afficheurs. Changement immédiat et mémorisé ; « Rétablir » revient aux couleurs d'origine.
3. **Réglages avancés du poste** — le catalogue complet des commandes CI-V (530 entrées, dont tout le menu SET) avec recherche, lecture et écriture. À utiliser avec précaution.
4. **Monitoring et détection CI-V** — journal des trames CI-V, adresse CI-V détectée, et recherche automatique du poste (voir « Dépannage »).
5. **Réglage Molette souris** — « Vitesse Potentiomètre » : 1 à 10 % de la course par cran de molette (2 % par défaut).

## 8. Exporter / importer la configuration (bouton ⇅)

- **Exporter** : enregistre dans un fichier `.ini` trois blocs : (1) tout ce qui concerne l'application (fenêtre, palettes, couleurs, verrous, vitesse de molette, mémoires, macros, indicatif, port COM…) ; (2) si le poste est connecté, les réglages affichés du poste (fréquences, modes, filtres, niveaux, NR/NB/notch, préampli, atténuateur, AGC, puissance, micro, compresseur, VOX, scope) ; (3) le **menu interne de l'IC-7760 (SET)** : toutes les entrées numériques du menu sont lues sur le poste une à une (quelques secondes, bouton « Arrêter » possible, barre d'état « Lecture du menu du poste n / N ») ; (4) les **mémoires de fréquences du poste** (canaux 1 à 99 et bords de scan P1/P2), lues telles que le poste les envoie.
- **Importer** : après le choix du fichier, des **cases à cocher** proposent de restaurer séparément : les réglages de l'application (le logiciel redémarre), les réglages affichés du poste, le menu interne de l'IC-7760, les mémoires de fréquences du poste (case décochée par défaut : elle **écrase** les canaux concernés du poste). Les réglages du poste partent tout de suite si le poste est connecté, sinon dès la connexion (environ 6 secondes après). Le menu est renvoyé entrée par entrée, en quelques secondes.
- L'accord de l'antenne et l'émission ne sont **jamais** rejoués.
- Ne sont **pas** inclus : les entrées de menu non numériques (formats « voir p.xx » du guide : filtres RX HPF/LPF, noms, textes…). Les canaux de mémoire vides ne sont pas exportés (un canal vide dans le fichier ne vide donc pas le canal du poste à l'import). Le format des mémoires n'a pas été vérifié sur un poste réel. Pour une sauvegarde complète du poste, utilisez la fonction de sauvegarde du poste lui-même (carte SD / clé USB).

## 9. CW, mémoires vocales, bandes

- **CW** : vitesse, tonalité, délai BK, texte libre (Envoyer), quatre messages F1-F4 (clic droit pour modifier, `{C}` = votre indicatif), Échap = stop.
- **Mémoires vocales T1-T8** et STOP.
- **Bandes** : 1,8 à 50 MHz et WWV ; chaque bande retient sa dernière fréquence et son mode.
- **Mémoires** : appel, MÉM, ÉCRIRE, M→V, EFFACER, VFO, SCAN.

## 10. Verrous, molette, limite de puissance

- **Verrouiller (clic droit)** : un clic droit sur un bouton ou un potentiomètre le **verrouille** : il devient rouge, ne réagit plus (clic, glisser, molette) et n'envoie aucune commande. Nouveau clic droit = déverrouillage. Un potentiomètre verrouillé verrouille aussi ses touches associées (préréglages de largeur de filtre, de puissance, RIT 0, AUTO du niveau) ; verrouiller une touche ne verrouille pas le potentiomètre. Les verrous sont mémorisés et retrouvés au redémarrage. La couleur se change dans Options > Couleurs de l'interface > Verrouillage. (VITESSE, MOY, PICS, F1-F4 et les boutons STOP gardent leur clic droit habituel.)
- **Vitesse de la molette** : Options (⚙) > **Réglage Molette souris** : « Vitesse Potentiomètre » règle de 1 à 10 % de la course par cran de molette (2 % par défaut). Maj + molette = pas plus fin.
- **Limiter la puissance (P.MAX)** : Dans le cadre Émission, la touche **P.MAX** demande une puissance maximale (1 à 200 W). Le potentiomètre PUISS. ne peut plus la dépasser et les préréglages supérieurs (10 / 50 / 100 / 200 W) sont grisés. Si le poste émet déjà au-delà, il est ramené à la limite.

## 11. Dépannage

### « Aucune réponse du poste »
1. Vérifiez le **port COM** (Gestionnaire de périphériques > Ports) et que le poste est allumé. Fermez les autres logiciels qui utilisent le port.
2. Ouvrez **⚙ > Monitoring et détection CI-V**. La ligne d'état indique l'adresse CI-V détectée sur la liaison :
   - en vert : adresse détectée ;
   - en orange : « le poste répond en 94h mais le logiciel écrit à B2h » → corrigez l'adresse dans la fenêtre principale ;
   - « Aucune réponse » → lancez **Détecter le poste…**.
3. **Détecter le poste…** (poste déconnecté, port COM choisi) essaie 7 vitesses (115200, 57600, 38400, 19200, 9600, 4800, 230400), lit l'identifiant du poste (lecture seule) et propose de reporter l'adresse et la vitesse trouvées.
4. Dans le poste : vérifiez CI-V USB (Unlink si possible), vitesse, « CI-V USB Echo Back » = OFF.

### La cascade ne s'affiche pas
Vérifiez d'abord l'adresse CI-V (ci-dessus) : une mauvaise adresse empêche la plupart des commandes. Si la cascade reste vide malgré une liaison correcte, enregistrez le journal du monitoring et ouvrez une [issue](https://github.com/F5pbgLudovic/IC7760Link/issues).

### Diagnostic pour le développeur
Sans ouvrir le logiciel : `Diagnostic.bat` (ou `IC7760Link.exe --diag=COM5,115200,94`) enregistre 12 secondes de trames CI-V (dont les trames brutes de la cascade) dans `diagnostic-ci-v.txt`, à joindre à une issue.

## 12. Ligne de commande (avancé)

| Option | Effet |
|---|---|
| `--virtual` | Démarre directement sur le poste virtuel. |
| `--selftest=rapport.txt` | Autotest complet contre le poste virtuel ; écrit le rapport. |
| `--diag=COM5,115200,94` | Enregistre 12 s de trames CI-V dans `diagnostic-ci-v.txt`. |
| `--shot=image.png,ms,actions` | Capture d'écran automatique (développement). |

## 13. À savoir

- Le logiciel a été développé contre un **poste virtuel** ; premiers essais sur un IC-7760 réel : connexion, affichage de la cascade et du S-mètre confirmés. Le reste (écriture des réglages, émission) reste à valider poste par poste — **testez l'émission sur charge fictive** et à faible puissance.
- Le format de la cascade (trames `27 00`) est confirmé sur un poste réel ; l'indexation de la largeur de filtre (`1A 03`) est encore supposée identique à la famille IC-7300/7610.
- Projet indépendant, **non affilié à ICOM**. Licence GPL v3.
