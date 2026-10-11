# Validation du MVP — à exécuter sur Windows

Statut initial : **aucun essai Windows, matériel ou SolidWorks effectué**. Les tests C# sont
fournis mais n'ont pas été exécutés sur l'environnement Linux de création. La revue structurelle
ne remplace pas une compilation. Utiliser une pièce de test, puis un assemblage représentatif.

## Fiche d'essai

- Windows / build :
- Visual Studio / SDK .NET :
- SolidWorks / service pack / chemin interops :
- Souris principale / connexion :
- Trackball / connexion / VID / PID / chemin :
- KensingtonWorks ou autres outils de remappage :
- Résultats MSBuild diagnostic / tests / add-in :
- Fréquence appliquée typique et minimale en rotation continue :
- Effets de bord des boutons / molette :
- Latence et dérive du curseur observées :

## A — lecture indépendante (avant activation CAD)

1. Compiler le diagnostic seul et les tests; exécuter les tests, exiger le code de sortie zéro.
2. Lancer le diagnostic, bouger uniquement la souris principale, noter handle et chemin.
3. Bouger uniquement la trackball : une autre ligne doit recevoir dx/dy.
4. Tester les deux en même temps; chaque ligne doit recevoir les événements correspondants.
5. Tester boutons, deux sens de molette et petits incréments éventuels. Les flags représentent
   les transitions, pas un masque continu de boutons tenus. Un périphérique absolu ne navigue pas.
6. Débrancher/rebrancher : aucune ancienne sélection ne doit se réactiver; sélectionner à nouveau.
7. Vérifier qu'aucun événement d'une souris non sélectionnée n'entre dans la navigation CAD.

Échec si une collection HID fusionne les deux périphériques. Ne pas masquer cet échec par le nom.

## B — rotation API

1. Compiler contre les interops réels, enregistrer x64, charger l'add-in; noter toute erreur.
2. Tester connecté sans document : aucune rotation, aucune exception affichée par SolidWorks.
3. Ouvrir une pièce, revenir au premier plan, appuyer sur le bouton choisi et tourner X puis Y.
   Vérifier axes, sens, sensibilité et centre de rotation. Aucun mouvement sans le bouton.
4. Tourner en continu : relever Hz réellement appliqués, fluidité et retard; tester aussi
   un assemblage lourd et une trackball à polling élevé.
5. Relâcher; aucun mouvement résiduel. Échap et Alt-Tab annulent sans reprise automatique.
6. Fermer/changer le document et changer la vue pendant un geste : aucune ancienne trame
   rejouée; il faut relâcher et presser à nouveau. Dessin / view-only restent inactifs.
7. Tester commandes, esquisse, dialogue modal et menus; noter toute rotation inappropriée.
8. Fermer le diagnostic, décharger le complément, recharger, quitter SolidWorks et reconnecter.
   Aucun thread bloquant la sortie, aucun confinement persistant et aucune rotation retardée.

## C — curseur et deux mains (critère principal non acquis)

1. Sans confinement : sélectionner et manipuler avec la souris droite; tourner la boule gauche.
   Mesurer le déplacement parasite du curseur. Il est attendu puisque Raw Input ne le supprime pas.
2. Avec confinement : le curseur reste fixe pendant le geste, mais la souris droite est contrainte.
   Son premier événement annule le geste; vérifier qu'elle retrouve immédiatement son usage.
3. Tester relâchement, Échap, Alt-Tab, retrait USB, arrêt du client et limite de 5 s.
4. Vérifier les actions Windows déclenchées par le bouton natif et par la molette pendant le geste.
5. Essayer plusieurs écrans et DPI; noter les transitions et éventuels sauts à la libération.

**Réussite finale seulement si la souris droite reste pleinement utilisable pendant la rotation,
sans déplacement parasite ni clic indésirable de la trackball. Le prototype actuel ne garantit
pas ce résultat et son mode ClipCursor ne peut pas, à lui seul, satisfaire ce critère.**

Ne pas déclarer la preuve de concept complète sur la seule réussite de A et B. Documenter le
compromis acceptable ou valider une autre stratégie d'isolation avant les fonctions avancées.
