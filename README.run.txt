#OneDriveCli Programmstart

Im Ordner BinaryRelease liegt eine Zipdatei, welche eine fertig kompilierte und lauffähige Version enthält.

Wenn das Programm ohne Parameter gestartet wird erscheint die Hilfe.
Es muss mindestens
- ein lokales Verzeichnis und die
- Synchronisierungsrichtung angegeben werden.

OneDriveCli.exe -l C:\Users\Benutzer\OneDriveCli -s r2l
=> Synchronisiert das OneDrive Verzeichnis nach C:\Users\Benutzer\OneDriveCli (eine Richtung!)

Beim Start muss die Berechtigung für den OneDrive Account geladen werden.
Es wird versucht ein Browserfenster zu öffnen, in welchem die Microsoft Account Daten (jene die
auch zur OneDrive Anmeldung verwendet werden) eingetragen werden. Im zweiten Schritt muss
OneDriveCli (bzw. bei Selbsterstellung aus den Quelldateien der Name der im Microsoft
Application Registration Portal angegebenen Namen) berechtigt werden. Wenn dies erfolgt ist,
erscheint eine kurze Meldung im Browser und das Browserfenster kann geschlossen werden.
Die Zugriffsberechtigung für OneDriveCli wird gespeichert und die synchnoisierung startet.

Falls kein Browser gestartet werden konnte, wird die aufzurufende URL (kann auch auf einem
anderen Gerät aufgerufen werden) ausgegeben mit welcher die Berechtigung für OneDriveCli erteilt
werden kann. Im Anschluss muss jedoch die URL der FEHLERSEITE (im Browser erscheint im Regelfall
ein Zeitüberschreitungsfehler) wieder in OneDriveCli eingetragen werden. Diese URL enthält den
für den Zugriff notwendigen Token-Code.

Linux, Mac, ... x86, x64, ARM, ...
OneDriveCli wurde basierend auf das .NET Framework entwickelt/erstellt. Für die diversen
Plattformen ist in der Regel "mono" verfügbar. Mithilfe dieses "Tools" sollte OneDriveCli
auf allen möglichen Plattformen laufen: "mono OneDriveCli.exe"
Bei Microsoft Windows ist .NET bereits vorinstalliert wesshalb es sofort laufen sollte.

OneDriveCli Parameter: wenn OneDriveCli ohne Parameter gestartet wird und config.xml nicht
existiert erscheint eine Hilfemeldung. Diese kann auf per -h Schalter aufgerufen werden.
Besonderheiten:
Es gibt den Switch "-test": damit werden keine Dateiänderungen im Ziel/Quellordner durchgeführt
Mit -fe und -ff können einschliessende als auch ausschliessende Pfad/Dateifilter angegeben werden.
ACHTUNG: Regular Expression Syntax, die Ordner müssen ab dem Hauptverzeichnis (/) zutreffen!
Es ist auch unter Windows nur der Pattern mit / als Pfadtrenner zu definieren!





