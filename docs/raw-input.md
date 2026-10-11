# Raw Input et curseur

La fenêtre message-only du processus UI enregistre usage page 1 / usage 2 (souris), avec
`RIDEV_INPUTSINK | RIDEV_DEVNOTIFY`. Aucune utilisation de `RIDEV_NOLEGACY` ou `RIDEV_CAPTUREMOUSE`.
Chaque `WM_INPUT` est lu avant `DefWindowProc` et contient le `hDevice` qui permet de filtrer
une souris physique parmi les collections HID exposées par Windows.

L'entête `RAWINPUTHEADER` vaut 24 octets en x64. `RAWMOUSE` vaut 24 octets; son union de
boutons commence à l'offset 4, dx à 12 et dy à 16. La taille du tampon vient de
`GetRawInputData`; le tampon natif est réutilisé. Les erreurs Win32 apparaissent dans l'UI.
Les unités sont des comptes relatifs, pas des pixels du curseur. Les mouvements absolus
s'affichent mais ne pilotent pas la rotation. `hDevice == 0` n'est pas sélectionnable.
La molette verticale et horizontale est décodée en `short` signé, sans supposer un pas de 120.
Les boutons sont des transitions; `GestureGate` conserve l'état du bouton d'activation.

Les changements de périphériques annulent le geste, réénumèrent et retirent la sélection.
Les valeurs du diagnostic sont les dernières par périphérique; les compteurs incluent tous
les paquets. Rafraîchissement visuel à 10 Hz pour éviter une ligne UI par événement.

## Limite fondamentale du prototype

Raw Input observe les entrées. Il ne neutralise pas la contribution d'une seule souris au
curseur partagé. `RIDEV_NOLEGACY` s'applique à l'enregistrement d'une collection, sans paramètre
`hDevice` sélectionné. Il n'est pas utilisé comme filtre physique individuel.

`ClipCursor` contraint le curseur partagé : la souris principale ne reste pas libre pendant
le confinement. L'option expérimentale est désactivée par défaut, maintenue par le bouton,
annulée sur relâchement, autre souris, Échap, perte de focus, perte IPC, fermeture ou 5 s.
L'activité d'une autre souris annule la contrainte mais le premier mouvement peut déjà
avoir été perdu; elle ne réalise pas l'usage simultané demandé. Le contrôle périodique dépend
de la boucle UI Windows : il n'est pas une garantie temps réel lors d'un blocage du processus.
Les clics et la molette natifs ne sont jamais supprimés.

Sources :
- [Using Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/using-raw-input)
- [RAWMOUSE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawmouse)
- [WM_INPUT et nettoyage](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-input)
- [ClipCursor, ressource partagée](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-clipcursor)
