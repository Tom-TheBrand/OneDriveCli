# OneDriveCli - Kommandozeilenbasierte Synchronisation für Microsoft OneDrive.

Was ist das?
Kommandozeilen basierter OneDrive Client für Microsofts OneDrive (vormals SkyDrive).

Warum das?
Das vom Microsoft zur Verfügung gestellte OneDrive Tool hat den Nachteil, dass es
einerseits nur unter Windows funktioniert und andererseits nur für den lokalen
Windows Account vorgesehen ist.

Wie kam es dazu?
Ich habe 2 OneDrive Accounts und suchte nach einer Möglichkeit, einzelne/bestimmte
Dateien von einem Account automatisiert auf den anderen Account zu übertragen und
das am besten auf meiner NAS die mit Linux betrieben wird (die dann auch noch ein
Versionsbasiertes Backup beider Accounts erstellt).
Inspiriert wurde ich von onedrive-d, ein perl basiertes Tool. Leider klappte dieses
bei mir nicht wesshalb ich OneDriveCli ins Leben rief.

Was kann es?
Es war als kleines Tool gedacht um Daten in eine Richtung zu Synchronisieren.
Mittlerweile kann es aber auch in beide Richtungen synchronisieren und das sehr
erfolgreich: Lokal -> OneDrive, OneDrive -> Lokal und OneDrive <> Lokal
Letzteres synchronisiert die jüngere Datei an das andere Ende - ein Zusammenführen
von beidseitigen Änderungen ist nicht vorgesehen.
