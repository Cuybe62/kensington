# Détection et identité

`GetRawInputDeviceList` énumère les collections, `GetRawInputDeviceInfo(RIDI_DEVICENAME)`
lit leur chemin symbolique Unicode. L'énumération retente en cas d'arrivée entre comptage
et lecture. Une collection disparue pendant la lecture est ignorée.

`RawInputDevice` contient handle courant, chemin, VID et PID extraits du chemin HID lorsqu'ils
existent. Certains périphériques Bluetooth, virtuels ou distants n'exposent pas ces identifiants
sous la même forme : les champs restent alors vides. Aucun modèle Kensington n'est codé en dur.

Le nom affiché est volontairement un libellé générique « HID mouse VID:PID ». Le MVP n'interroge
pas encore SetupAPI pour un nom commercial. Identifier la Kensington en faisant bouger la boule
seule et en comparant la ligne dont le compteur augmente; faire la même chose avec la souris.
Le diagnostic affiche les chemins complets et handles, y compris pour les dispositifs non choisis.

Le handle n'est valide que pour la session et peut être recyclé. Le chemin est une meilleure
clé, mais peut changer avec le port, la pile de pilotes ou la réinstallation. VID/PID ne sépare
pas deux exemplaires identiques. `MatchesExact` refuse les chemins vides; aucun rapprochement
ambigu ni aucune sélection automatique ne sont effectués.

Les paramètres du MVP restent en mémoire. La sauvegarde JSON/XML, nom SetupAPI, numéro de
série/ContainerId et confirmation des correspondances ambiguës appartiennent à la deuxième étape.
Un récepteur unifié qui fusionne plusieurs périphériques en une seule collection ne permet pas
nécessairement de les distinguer par cette méthode. Tester aussi l'effet de KensingtonWorks.

[Référence GetRawInputDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getrawinputdeviceinfow).
