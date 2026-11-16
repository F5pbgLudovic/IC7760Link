# ADR 0002 — Développement contre un poste virtuel, jamais de vraie station

- **Statut** : accepté (2026-11-12)
- **Contexte** : le poste n'est pas branché chez le développeur ; l'IC-7760 appartient à un ami.
- **Décision** : `VirtualRadio.cs` simule un IC-7760 (deux récepteurs, spectre vivant, compteurs, commandes `29`, scope `27`) derrière l'interface `ILink`. `--selftest=rapport.txt` exécute un autotest complet contre ce poste ; `--shot=…` produit des captures automatiques ; les essais d'émission ne se font jamais sur une vraie station.
- **Conséquences** : tout comportement nouveau doit être reproduit dans le poste virtuel pour rester testable. Le test « PTT retour d'état » peut échouer de façon aléatoire quand la machine est chargée (relancer).
