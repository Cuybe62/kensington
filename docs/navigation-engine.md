# Moteur de navigation

Le MVP n'applique qu'une sensibilité linéaire (0,002 rad/compte par défaut). Aucun lissage,
aucune inertie ni accélération ne cachent la qualité du signal brut.

`GestureGate` exige une transition down du bouton sélectionné pendant que SolidWorks est au
premier plan et que l'utilisateur a activé la rotation. La perte d'autorisation annule le geste;
une reprise de focus ne le réarme pas. Un up puis un nouveau down sont nécessaires.

`MotionAccumulator` additionne X/Y sous un verrou et les consomme atomiquement. Un reset ou
une trame inactive détruit les déplacements en attente. Les composantes sont bornées à 100 rad
pour éviter une accumulation illimitée, puis le moteur limite chaque mise à jour à ±0,25 rad.
C'est une protection contre un saut, pas une courbe d'accélération; de grands mouvements peuvent
être tronqués. Les trames non finies et hors bornes sont rejetées par le protocole.

Le worker UI consomme à intervalle demandé 16 ms, écrit une trame et attend un accusé (disponibilité
CAD et compteur de rotations). Il ne fait ni accès UI ni COM. Un seul échange est en vol.
Le worker add-in valide la trame et accumule, sans toucher au CAD. Le timer STA consomme au plus
une fois par tick (intervalle 16 ms, garde de 120 Hz); aucun rattrapage des ticks manqués.
La fréquence réelle peut être inférieure à 60 Hz, notamment avec la granularité WM_TIMER.

Protocole binaire v1 little-endian : magic Int32 `0x54424344`, version Int32 `1`, UTC ticks Int64,
X Float64, Y Float64, actif Bool (33 octets). Réponse : disponible Bool, rotations Int32 (5 octets).
Les timestamps d'émission expirent à 150 ms; un timestamp futur est refusé. La correction de
l'horloge peut annuler un geste. La reconnexion efface les anciens deltas. Le canal est nommé
avec SID et PID SolidWorks, et son ACL autorise seulement l'utilisateur courant avec refus réseau.
Il ne constitue pas une frontière de sécurité contre les autres applications du même utilisateur.

Après changement de document/vue ou interruption côté add-in, une trame inactive est requise
avant une nouvelle rotation. Les deltas antérieurs ne sont pas rejoués dans le nouveau document.

Extensions futures : filtres purs sur une trame avec durée explicite, gestes séparés du transport,
capacités CAD supplémentaires avec tests. Ne pas ajouter de méthodes CAD non implémentées qui
échoueraient silencieusement. Les tests actuels couvrent identité, activation, concurrence,
consommation, protocole, péremption, absence d'appels inactifs et bornage des rotations.
