# ADR 0009 — Export/import : menu interne et mémoires du poste, import par cases à cocher

Date : 2026-11-19. Statut : adopté.

## Contexte
L'export (ADR 0006) ne couvrait que la configuration du logiciel et les réglages affichés. L'utilisateur veut aussi le menu interne de l'IC-7760 (SET) et les mémoires de fréquences, avec choix à l'import.

## Décision
- Clés du fichier `.ini` par nature : application (sans préfixe), `radio.*` (réglages affichés), `menu.*` (entrées `1A 05 …` numériques du catalogue, `Rig.ReadMenu/WriteMenu`), `memch.N` (canaux 1-99, 100/101 = bords de scan P1/P2 ; `Rig.ReadMemories/WriteMemories`). `Cfg.Kind()` / `Cfg.ImportParts(texte, bool[4])`.
- Mémoires : le format de `1A 00` n'étant pas dans le catalogue, les octets de données sont conservés bruts (hexa) et renvoyés à l'identique ; les canaux vides (`FF`) ne sont pas exportés.
- Import : cases à cocher (application = redémarrage ; réglages, menu, mémoires = envoi au poste à la connexion via `*.pending`, écritures espacées 35 ms / 120 ms). Mémoires décochées par défaut (elles écrasent le poste).
- Hors périmètre : entrées de menu non numériques (« voir p.xx »). Sauvegarde complète = celle du poste (SD/USB).

## Conséquences
Autotest sur poste virtuel (menu 232 entrées, mémoires). Format réel des mémoires et écriture non validés sur poste réel.