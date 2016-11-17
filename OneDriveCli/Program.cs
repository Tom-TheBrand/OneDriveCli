using Microsoft.OneDrive.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OneDriveCli
{
    class Program
    {
        // https://dev.onedrive.com/README.htm
        // https://github.com/OneDrive/onedrive-sdk-dotnet-msa-auth-adapter
        // https://github.com/OneDrive/onedrive-sdk-csharp

        const string clientId = "";
        const string clientSecret = "";

        static SyncMode syncMode = SyncMode.OneDrive2Local;
        static string configFileName = "config.xml";
        static Config config;
        static bool verboseLogging = false;
        static Regex rFileInclude = null;
        static Regex rFileExclude = null;

        static void addLog(bool isVerbose, string Message, bool addDateTime = false)
        {
            if (config.Options.LogFile == null || (!verboseLogging && isVerbose)) return;

            lock (config.Options.LogFile)
                using (StreamWriter sw = new StreamWriter(System.IO.File.Open(config.Options.LogFile, FileMode.OpenOrCreate | FileMode.Append, FileAccess.Write)))
                    sw.WriteLine((addDateTime ? DateTime.UtcNow.ToString("s") + "\t" : "") + Message);
        }

        static void saveConfig()
        {
            using (System.IO.FileStream fs = System.IO.File.Open(configFileName, FileMode.Create, FileAccess.Write))
                (new DataContractSerializer(typeof(Config))).WriteObject(fs, config);
        }

        static void Main(string[] args)
        {
            OneDriveClient odc = null;
            bool showUsage = false;
            string sError = null;

            bool quietLog = false;
            bool syncErrorOccured = false;
            bool testMode = false;
            string logFileName = null;
            bool useNoWebSocket = false;
            bool syncModeParameter = false;
            bool newConfig = false;
            string remoteBaseDir = null;
            string localDir = null;
            string sFileInclude = null;
            string sFileExclude = null;

            #region Parameter auswerten, Fehler und Usage Behandlung
            for (int argc = 0; argc < args.Length; argc++)
            {
                if (args[argc] == "-c" && argc + 1 < args.Length)
                    configFileName = args[++argc];
                else if (args[argc] == "-rd" && argc + 1 < args.Length)
                {
                    remoteBaseDir = args[++argc].Replace("\\", "/");
                    if (!remoteBaseDir.EndsWith("/")) remoteBaseDir += "/";
                }
                else if (args[argc] == "-ld" && argc + 1 < args.Length)
                {
                    localDir = args[++argc];
                    DirectoryInfo di = new DirectoryInfo(localDir);
                    if (!di.Exists)
                        sError = "Lokales Verzeichnis existiert nicht!";
                    localDir = di.FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar.ToString();
                }
                else if (args[argc] == "-ff" && argc + 1 < args.Length)
                    sFileInclude = args[++argc];
                else if (args[argc] == "-fe" && argc + 1 < args.Length)
                    sFileExclude = args[++argc];
                else if (args[argc] == "-s" && argc + 1 < args.Length)
                {
                    syncModeParameter = true;
                    string mode = args[++argc].ToLower();
                    if (mode == "beide") syncMode = SyncMode.BothDirections;
                    else if (mode == "r2l") syncMode = SyncMode.OneDrive2Local;
                    else if (mode == "l2r") syncMode = SyncMode.Local2OneDrive;
                    else sError = "Ungültiger Wert bei -s!";
                }
                else if (args[argc] == "-n")
                    newConfig = true;
                else if (args[argc] == "-u")
                    useNoWebSocket = true;
                else if (args[argc] == "-l" && argc + 1 < args.Length)
                    logFileName = args[++argc];
                else if (args[argc] == "-v")
                    verboseLogging = true;
                else if (args[argc] == "-q")
                    quietLog = true;
                else if (args[argc] == "-test")
                    testMode = true;
                else
                    showUsage = true;
            }
            if (verboseLogging) quietLog = false;

            if (string.IsNullOrEmpty(sError))
            {
                if (!System.IO.File.Exists(configFileName)) newConfig = true;
                if (newConfig && string.IsNullOrEmpty(localDir))
                    sError = "Lokales Verzeichnis muss angegeben werden!";
                if (!string.IsNullOrEmpty(sFileInclude))
                    try
                    {
                        rFileInclude = new Regex(sFileInclude);
                    }
                    catch (Exception exr)
                    {
                        sError = "Regex \"" + sFileInclude + "\" ungültig (" + exr.Message + ")!";
                    }
                if (!string.IsNullOrEmpty(sFileExclude))
                    try
                    {
                        rFileExclude = new Regex(sFileExclude);
                    }
                    catch (Exception exr)
                    {
                        sError = "Regex \"" + sFileExclude + "\" ungültig (" + exr.Message + ")!";
                    }
            }
            if (!string.IsNullOrEmpty(sError))
            {
                Console.WriteLine("FEHLER: " + sError);
                showUsage = true;
            }
            if (showUsage)
            {
                Console.WriteLine("OneDriveCli.exe [-n] [-c config.xml] [-rd /Bilder] [-ld C:\\Users\\User\\OneDrive]");
                Console.WriteLine("                [-ff DateiFilter]  [-fe DateiFilter] [-s r2l|l2r|beide]");
                Console.WriteLine("                [-l logfile] [-v]");
                Console.WriteLine(" -c  ... Konfigurationsdatei (Vorgabewert: config.xml)");
                Console.WriteLine(" -u  ... Authentifizierung mit URL kopieren (im Konsolenmodus verwenden!)");
                Console.WriteLine(" -n  ... neue Konfiguration erstellen (default, wenn Konfigurationsdatei nicht");
                Console.WriteLine("         vorhanden ist). Angabe von -ld ist erforderlich!");
                Console.WriteLine(" -rd ... OneDrive Basis Verzeichnis");
                Console.WriteLine(" -ld ... Lokales Verzeichnis zum Synchronisieren");
                Console.WriteLine(" -ff ... Filter, welche Dateien synchronisiert werden sollen (RegEx)");
                Console.WriteLine(" -fe ... Filter, welche Dateien NICHT synchronisiert werden sollen (RegEx)");
                Console.WriteLine(" -s  ... r2l: OneDrive > Lokal synchronisieren (Vorgabe)");
                Console.WriteLine("         l2r: Lokal > OneDrive synchronisieren");
                Console.WriteLine("         beide: in beide Richtungen synchronisieren");
                Console.WriteLine(" -l  ... Protokolldatei");
                Console.WriteLine(" -v  ... Detailiert protokollieren");
                Console.WriteLine(" -q  ... Weniger protokollieren");
                Console.WriteLine(" -test . Sync. testen (ACHTUNG: Konfiguration wird nicht gespeichert!)");
                Console.WriteLine("         Es werden keine lokalen/remote Änderungen im Dateisystem vorgenommen");
                Console.WriteLine("Die Parameter rd,ld,ff,fe,s werden in die Konfigurationsdatei gespeichert,");
                Console.WriteLine("müssen somit nicht angegeben werden, überschreiben aber bereits vorhandene");
                Console.WriteLine("Werte!");
                Console.WriteLine("ACHTUNG: wenn die Filter Pfadtrenner enthalten, so müssen diese mit \\ UND /");
                Console.WriteLine("zutreffen, damit diese lokal und im OneDrive gültig sind!");

                if (Debugger.IsAttached)
                    Console.ReadLine();
                return;
            }
            #endregion

            #region Konfigurationsdatei laden, neue Parameter in config Objekt speichern
            config = new Config();
            if (!newConfig && System.IO.File.Exists(configFileName))
                using (System.IO.FileStream fs = System.IO.File.OpenRead(configFileName))
                    config = (Config)(new DataContractSerializer(typeof(Config))).ReadObject(fs);

            if (config.Options == null) config.Options = new SyncOptions();
            if (config.fileStati == null) config.fileStati = new Dictionary<string, FileHistory>();

            if (logFileName != null)
                config.Options.LogFile = logFileName != "" ? logFileName : null;
            if (syncModeParameter)
                config.Options.SyncMode = syncMode;
            if (!string.IsNullOrEmpty(localDir))
                config.Options.LocalDir = localDir;
            if (!string.IsNullOrEmpty(remoteBaseDir))
                config.Options.RemoteBaseDir = remoteBaseDir;

            if (sFileInclude != null) config.Options.FilterInclude = sFileInclude != "" ? sFileInclude : null;
            if (sFileExclude != null) config.Options.FilterExclude = sFileExclude != "" ? sFileExclude : null;
            if (!string.IsNullOrEmpty(config.Options.FilterInclude)) rFileInclude = new Regex(config.Options.FilterInclude, RegexOptions.Compiled);
            if (!string.IsNullOrEmpty(config.Options.FilterExclude)) rFileExclude = new Regex(config.Options.FilterExclude, RegexOptions.Compiled);
            #endregion

            OdcAuthenticationProvider oa = new OdcAuthenticationProvider(clientId, clientSecret, config.authentication);

            #region (erst) Authentifizierung
            while (config.authentication == null)
            {
                Regex rCode = new Regex("code=([^=]+)$");
                HttpListener hl = null;
                Match mCode;

                Uri authUri;
                string authCode;
                string tokenUrl = null;
                bool isListening = false;
                Process pAuthorizationBrowser = null;

                Console.WriteLine("Live-Konto Authentifizierung wird gestartet...");
                authUri = oa.AuthenticationUrl();
                if (!useNoWebSocket)
                {
                    try
                    {
                        hl = new HttpListener();
                        hl.Prefixes.Add("http://localhost:8888/");
                        hl.Start();
                        isListening = true;
                    }
                    catch
                    {
                        Console.WriteLine("> WARNUNG: http-Bindung konnte nicht gestartet werden. Authentifizierung muss durch manuelles kopieren der URL's erfolgen!");
                    }
                    pAuthorizationBrowser = Process.Start(authUri.ToString());
                }

                if (pAuthorizationBrowser == null || !isListening)
                {
                    Console.WriteLine(authUri.ToString() + "\n" +
                                      "Bitte obige URL in einen Browser kopieren und Anwendung berechtigen. Es wird auf eine URL umgeleitet " +
                                      "bei der eine Fehlermeldung angzeigt wird, diese dennoch hier wieder einfügen!\nBeispiel URL: http://localhost:8888/onedrivecli?code=XYZ");
                    tokenUrl = Console.ReadLine();
                }
                else
                {
                    Task t = Task.Run(() =>
                    {
                        HttpListenerContext ctx = hl.GetContext();
                        tokenUrl = ctx.Request.Url.OriginalString;
                        ctx.Response.StatusCode = 200;
                        using (StreamWriter sw = new StreamWriter(ctx.Response.OutputStream))
                            sw.Write("<html><body>Token erfolgreich in OneDriveCli geladen, Browserfenster kann geschlossen werden. Weitere Schritte siehe OneDriveCli!</body></html>");
                        ctx.Response.Close();
                        hl.Stop();
                        return true;
                    });
                    Console.WriteLine("> INFO: Browserfenster wurde gestartet! Bitte Authentifizierung für OneDriveCli durchführen!");
                    t.Wait();
                }

                mCode = rCode.Match(tokenUrl);
                if (!mCode.Success)
                {
                    Console.WriteLine("URL ist ungültig! Erneut versuchen? [J/n]");
                    if (Console.ReadKey().KeyChar != 'n')
                        continue;
                    return;
                }

                authCode = mCode.Success ? mCode.Groups[1].Value : null;
                config.authentication = oa.AuthenticateTokenAsync(authCode).Result;

                if (config.authentication == null)
                {
                    Console.WriteLine("Authentifizierung fehlgeschlagen! Erneut versuchen? [J/n]");
                    if (Console.ReadKey().KeyChar != 'n')
                        continue;
                    return;
                }
                Console.WriteLine("> INFO: Authentifizierungs Token erfolgreich empfangen!");
            }
            #endregion

            saveConfig();

            if (!quietLog)
            {
                addLog(false, "Start der Synchronisation:", true);
                addLog(false, "o\tLokales Verzeichnis: " + config.Options.LocalDir.Replace("/", Path.DirectorySeparatorChar.ToString()));
                addLog(false, "o\tOneDrive Verzeichnis: " + (config.Options.RemoteBaseDir ?? "/"));
                addLog(false, "o\tFilter (einschliessend): " + (config.Options.FilterInclude ?? "-"));
                addLog(false, "o\tFilter (ausschliessend): " + (config.Options.FilterExclude ?? "-"));
                addLog(false, "o\tSynchronisierung: " + (config.Options.SyncMode == SyncMode.BothDirections ? "beide Richtungen" : (config.Options.SyncMode == SyncMode.Local2OneDrive ? "Lokal > OneDrive" : "OneDrive > Lokal")));
            }

            try
            {
                odc = new OneDriveClient("https://api.onedrive.com/v1.0", oa);
                Item d = odc.Drive.Root.ItemWithPath(config.Options.RemoteBaseDir).Request().GetAsync().Result;

                if (!quietLog) addLog(true, ">\tPrüfe lokales Verzeichnis: " + config.Options.LocalDir);
                Console.WriteLine("Prüfe lokales Verzeichnis: " + config.Options.LocalDir);
                scanLocalFolder(config.Options.LocalDir);

                if (!quietLog) addLog(true, ">\tPrüfe OneDrive Verzeichnis: " + config.Options.RemoteBaseDir);
                Console.WriteLine("Prüfe OneDrive Verzeichnis: " + config.Options.RemoteBaseDir);
                scanRemoteFolder(odc, d, config.Options.RemoteBaseDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Erstellen der Strukturlisten (" + ex.Message + ")!");
                addLog(false, ">\tFehler beim Erstellen der Strukturlisten (" + ex.Message + ")");
                syncErrorOccured = true;
            }

            if (!syncErrorOccured)
            {
                if (!quietLog) addLog(true, ">\tSuche Verzeichnis-/Datei-Differenzen");
                Console.WriteLine("Suche Verzeichnis-/Datei-Differenzen");

                //List<string> parentsToUpdate = new List<string>();
                foreach (KeyValuePair<string, FileHistory> item in config.fileStati.Where(h => (h.Value.localSeen || h.Value.remoteSeen) && testFilePattern(h.Key, true) && h.Key != config.Options.LocalDir).OrderBy(h => h.Key.Length))
                {
                    if (item.Value.SkipSync) continue;

                    SyncDirection sd = SyncDirection.NotModified;
                    string relPath = item.Key.Substring(config.Options.LocalDir.Length - 1);
                    bool isDirectory = item.Key.EndsWith(Path.DirectorySeparatorChar.ToString());
                    StringBuilder sbStat = new StringBuilder();

                    sbStat.AppendFormat("Eintrag: \"{0}\"", relPath);

                    #region Synchronisierungsrichtung bestimmen
                    if (config.Options.SyncMode == SyncMode.Local2OneDrive)
                    {
                        if (item.Value.localSeen != item.Value.remoteSeen)
                            sd = SyncDirection.Lokal2Online;
                        else if (item.Value.localChanged && (!isDirectory || !item.Value.remoteSeen))
                            sd = SyncDirection.Lokal2Online;
                    }
                    else if (config.Options.SyncMode == SyncMode.OneDrive2Local)
                    {
                        if (item.Value.localSeen != item.Value.remoteSeen)
                            sd = SyncDirection.Online2Lokal;
                        else if (item.Value.remoteChanged && (!isDirectory || !item.Value.localSeen))
                            sd = SyncDirection.Online2Lokal;
                    }
                    else if (config.Options.SyncMode == SyncMode.BothDirections)
                    {
                        FileHistory fhParentDir = config.fileStati.First(h => h.Key == item.Key.Substring(0, item.Key.LastIndexOf(Path.DirectorySeparatorChar, item.Key.Length - 2) + 1)).Value;
                        if (!isDirectory)
                        {   // Datei             
                            if (item.Value.localSeen && !item.Value.remoteSeen)
                                sd = fhParentDir.remoteWriteTime > item.Value.localWriteTime && item.Value.remoteWriteTime > DateTime.MinValue ? SyncDirection.Online2Lokal : SyncDirection.Lokal2Online;
                            else if (item.Value.remoteSeen && !item.Value.localSeen)
                                sd = fhParentDir.localWriteTime > item.Value.remoteWriteTime && item.Value.localWriteTime > DateTime.MinValue ? SyncDirection.Lokal2Online : SyncDirection.Online2Lokal;
                            else if ((item.Value.remoteChanged || item.Value.localChanged) &&
                                item.Value.remoteSeen && item.Value.localSeen &&
                                item.Value.localWriteTime != item.Value.remoteWriteTime)
                                sd = item.Value.localWriteTime > item.Value.remoteWriteTime ? SyncDirection.Lokal2Online : SyncDirection.Online2Lokal;
                        }
                        else
                        {   // Verzeichnis
                            IEnumerable<KeyValuePair<string, FileHistory>> ifhYoungestWriteTime = config.fileStati.Where(f => f.Key.StartsWith(item.Key.Substring(0, item.Key.LastIndexOf(Path.DirectorySeparatorChar, item.Key.Length - 2) + 1)));
                            DateTime youngestLocalWriteTime = item.Value.localSeen ? ifhYoungestWriteTime.Max(f => f.Value.localWriteTime) : DateTime.MinValue;
                            DateTime youngestRemoteWriteTime = item.Value.remoteSeen ? ifhYoungestWriteTime.Max(f => f.Value.remoteWriteTime) : DateTime.MinValue;

                            if (item.Value.localSeen && !item.Value.remoteSeen)
                                sd = fhParentDir.remoteChanged && fhParentDir.remoteWriteTime > youngestLocalWriteTime ? SyncDirection.Online2Lokal : SyncDirection.Lokal2Online;
                            else if (item.Value.remoteSeen && !item.Value.localSeen)
                                sd = fhParentDir.localChanged && fhParentDir.localWriteTime > youngestRemoteWriteTime ? SyncDirection.Lokal2Online : SyncDirection.Online2Lokal;
                        }
                    }
                    sbStat.Insert(0, "*\t" + sd.ToString() + " ");
                    #endregion

                    #region Synchronisieren
                    try
                    {
                        if (sd == SyncDirection.Lokal2Online)
                        #region Operationen im Remote Dateisystem
                        {
                            if (item.Value.localSeen)
                            {
                                Item uploadedItem;
                                if (!isDirectory)
                                {
                                    Stopwatch sw = new Stopwatch();
                                    FileInfo fi = new FileInfo(item.Key);

                                    sw.Start();
                                    using (System.IO.FileStream fs = fi.Open(FileMode.Open, FileAccess.Read))
                                        uploadedItem = odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(item.Key)).Content.Request().PutAsync<Item>(fs).Result;
                                    sw.Stop();

                                    item.Value.remoteWriteTime = uploadedItem.LastModifiedDateTime.Value.UtcDateTime;
                                    item.Value.remote_cTag = uploadedItem.CTag;
                                    item.Value.remoteSeen = true;
                                    item.Value.remoteChanged = false;
                                    sbStat.AppendFormat(" Datei hochgeladen ({1}kB, {0}kB/s)", sw.ElapsedMilliseconds > 0 ? Math.Round((decimal)fi.Length / sw.ElapsedMilliseconds, 2).ToString() : "?", Math.Round((decimal)fi.Length / 1024, 0).ToString());
                                }
                                else
                                {
                                    uploadedItem = odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(item.Key)).Request().CreateAsync(new Item() { Folder = new Folder() }).Result;

                                    item.Value.remoteWriteTime = uploadedItem.LastModifiedDateTime.Value.UtcDateTime;
                                    item.Value.remoteSeen = true;
                                    item.Value.remoteChanged = false;
                                    sbStat.Append(" Verzeichnis erstellt");
                                }
                            }
                            else
                            {
                                if (!isDirectory)
                                {
                                    odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(item.Key)).Request().DeleteAsync().Wait();
                                    sbStat.Append(" Datei gelöscht");
                                    item.Value.SkipSync = true;
                                    item.Value.remoteSeen = false;
                                }
                                else
                                {
                                    odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(item.Key)).Request().DeleteAsync().Wait();
                                    sbStat.Append(" Verzeichnis gelöscht");

                                    foreach (KeyValuePair<string, FileHistory> kvp in config.fileStati.Where(f => f.Key.StartsWith(item.Key)))
                                    {
                                        kvp.Value.SkipSync = true;
                                        kvp.Value.remoteSeen = false;
                                    }
                                }
                            }
                        }
                        #endregion
                        if (sd == SyncDirection.Online2Lokal)
                        #region Operationen im Lokalen Dateisystem
                        {
                            if (item.Value.remoteSeen)
                            {
                                if (!isDirectory)
                                {
                                    if (!testMode)
                                    {
                                        Stopwatch sw = new Stopwatch();
                                        FileInfo fi = new FileInfo(item.Key);

                                        sw.Start();
                                        using (FileStream fs = fi.Open(FileMode.Create, FileAccess.Write))
                                        using (Stream s = odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(item.Key)).Content.Request().GetAsync().Result)
                                            s.CopyTo(fs);
                                        sw.Stop();
                                        fi.LastWriteTimeUtc = item.Value.remoteWriteTime;

                                        item.Value.localWriteTime = fi.LastWriteTimeUtc;
                                        item.Value.localSeen = true;
                                        item.Value.localChanged = false;
                                        item.Value.local_cTag = getLocalCTag(fi.FullName);
                                        sbStat.AppendFormat(" Datei downgeloadet ({1}kB, {0}kB/s)", sw.ElapsedMilliseconds > 0 ? Math.Round((decimal)fi.Length / sw.ElapsedMilliseconds, 2).ToString() : "?", Math.Round((decimal)fi.Length / 1024, 0).ToString());
                                    }
                                    else
                                        sbStat.AppendFormat(" Datei downgeloadet (0kB/s)");
                                }
                                else
                                {
                                    if (!testMode)
                                    {
                                        DirectoryInfo di = new DirectoryInfo(item.Key);
                                        di.Create();

                                        try
                                        {
                                            di.LastWriteTimeUtc = item.Value.remoteWriteTime;
                                        }
                                        catch
                                        {
                                            sbStat.Append("(WARNUNG: Bearbeitungszeit konnte nicht gesetzt werden)");
                                        }

                                        item.Value.localWriteTime = di.LastWriteTimeUtc;
                                        item.Value.localSeen = true;
                                        item.Value.localChanged = true;
                                    }
                                    sbStat.Append(" Verzeichnis erstellt");
                                }
                            }
                            else
                            {
                                if (!isDirectory)
                                {
                                    if (!testMode)
                                    {
                                        FileInfo fi = new FileInfo(item.Key);
                                        fi.Delete();
                                        item.Value.SkipSync = true;
                                        item.Value.localSeen = false;
                                    }
                                    sbStat.Append(" Datei gelöscht");
                                }
                                else
                                {
                                    if (!testMode)
                                    {
                                        DirectoryInfo di = new DirectoryInfo(item.Key);
                                        deleteSubDirectories(di);
                                        di.Delete();
                                    }

                                    foreach (KeyValuePair<string, FileHistory> kvp in config.fileStati.Where(f => f.Key.StartsWith(item.Key)))
                                    {
                                        kvp.Value.localSeen = false;
                                        kvp.Value.SkipSync = true;
                                    }
                                    sbStat.Append(" Verzeichnis gelöscht");
                                }
                            }
                        }
                        #endregion

                        //if (sd == SyncDirection.Lokal2Online || sd == SyncDirection.Online2Lokal)
                        //{
                        //    // im vezeichnis wurde eine änderung durchgeführt -> zum schluss die neuen zeiten rückladen, damit beim nächsten
                        //    // sync diese nicht als verändert gelistet werden
                        //    string pp = item.Key.Substring(0, item.Key.LastIndexOf(Path.DirectorySeparatorChar, item.Key.Length - 2) + 1);
                        //    while (pp.Length >= config.Options.LocalDir.Length)
                        //    {
                        //        if (!parentsToUpdate.Contains(pp))
                        //            parentsToUpdate.Add(pp);
                        //        if (pp == config.Options.LocalDir)
                        //            break;
                        //        pp = pp.Substring(0, pp.LastIndexOf(Path.DirectorySeparatorChar, Math.Max(pp.Length - 2, 0)) + 1);
                        //    }
                        //}
                    }
                    catch (Exception exSyncError)
                    {
                        sbStat.Append(" FEHLER: " + exSyncError.Message);

                        foreach (KeyValuePair<string, FileHistory> kvp in config.fileStati.Where(f => f.Key.StartsWith(item.Key)))
                            kvp.Value.SkipSync = true;
                    }
                    #endregion

                    addLog(sd == SyncDirection.NotModified, sbStat.ToString());
                }

                // nicht mehr vorhandene aus dem speicher entfernen
                while (config.fileStati.Any(h => !h.Value.localSeen && !h.Value.remoteSeen))
                    config.fileStati.Remove(config.fileStati.First(h => !h.Value.localSeen && !h.Value.remoteSeen).Key);

                //if (parentsToUpdate.Count > 0)
                //    try
                //    {
                //        addLog(true, ">\tAktualisiere Verzeichnisänderungen");
                //        foreach (string key in parentsToUpdate)
                //        {
                //            Item item = odc.Drive.Root.ItemWithPath(localFileName2RemoteFileName(key)).Request().GetAsync().Result;
                //            config.fileStati[key].remoteWriteTime = item.LastModifiedDateTime.Value.UtcDateTime;
                //            config.fileStati[key].localWriteTime = Directory.GetLastWriteTimeUtc(key);
                //            addLog(true, "c\t" + key + ": " + config.fileStati[key].remoteWriteTime.ToString() + " - " + config.fileStati[key].localWriteTime.ToString());
                //        }
                //    }
                //    catch (Exception exDC)
                //    {
                //        addLog(false, ">\tHINWEIS: Fehler beim Aktualisieren der Verzeichnisänderungen (" + exDC.Message + ")!");
                //    }

                if (!testMode && !syncErrorOccured)
                    saveConfig();
            }

            addLog(false, "Synchronisation fertig!", true);
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Fertig... Taste drücken!");
                Console.ReadKey();
            }
        }

        static private string localFileName2RemoteFileName(string localFile)
        {
            return config.Options.RemoteBaseDir + localFile.Substring(config.Options.LocalDir.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
        static private string remoteFileName2LocalFileName(string remoteFile)
        {
            return config.Options.LocalDir + remoteFile.Substring(config.Options.RemoteBaseDir.Length).Replace('/', Path.DirectorySeparatorChar);
        }
        static private bool testFilePattern(string fullFileName, bool localTest)
        {
            if (localTest)
                fullFileName = (Path.DirectorySeparatorChar.ToString() + fullFileName.Replace(config.Options.LocalDir, "")).Replace(Path.DirectorySeparatorChar, '/');

            if (rFileExclude != null && rFileExclude.IsMatch(fullFileName))
            {
                Console.WriteLine("> Überspringe (Filter) " + fullFileName);
                addLog(true, string.Format("-\tÜberspringe \"{0}\" (ausschliessender Filter erfüllt)", fullFileName));
                return false;
            }
            if (rFileInclude != null && !rFileInclude.IsMatch(fullFileName))
            {
                Console.WriteLine("> Überspringe (Filter) " + fullFileName);
                addLog(true, string.Format("-\tÜberspringe \"{0}\" (einschliessender Filter nicht erfüllt)", fullFileName));
                return false;
            }
            return true;
        }
        static private void deleteSubDirectories(DirectoryInfo di)
        {
            foreach (FileInfo fi in di.GetFiles())
            {
                addLog(false, "Entferne Datei (lokal): " + fi.FullName);
                fi.Delete();
            }
            foreach (DirectoryInfo dis in di.GetDirectories())
            {
                deleteSubDirectories(dis);
                addLog(false, "Entferne Verzeichnis (lokal): " + dis.FullName);
                dis.Delete();
            }
        }

        static private void scanLocalFolder(string localDir)
        {
            DirectoryInfo di;
            FileHistory fh;

            if (!testFilePattern(localDir, true))
                return;

            di = new DirectoryInfo(localDir);
            fh = setLocalFileEntry(localDir, di.LastWriteTimeUtc, false);
            addLog(!fh.localChanged, string.Format("l\tVerzeichnis \"{0}\" ({1}, {2})", (localDir.Substring(config.Options.LocalDir.Length - 1)), fh.localChanged ? "1" : "0", fh.localWriteTime.ToString()));

            foreach (DirectoryInfo dis in di.GetDirectories())
                scanLocalFolder(localDir + dis.Name + Path.DirectorySeparatorChar.ToString());

            foreach (FileInfo fis in di.GetFiles())
            {
                if (!testFilePattern(fis.FullName, true))
                    continue;

                fh = setLocalFileEntry(fis.FullName, fis.LastWriteTimeUtc, true);
                addLog(!fh.localChanged, string.Format("l\tDatei: \"{0}\" ({1}, {2}, {3})", fis.Name, fh.localChanged ? "1" : "0", fh.localWriteTime.ToString(), fh.local_cTag));
                //if (fh.localChanged)
                //    addLog(true, string.Format(" >> {0} - {1}", fis.LastWriteTimeUtc.ToString("hh:mm:ss.fff"), fh.localWriteTime.ToString("hh:mm:ss.fff")));
            }
        }
        static private string getLocalCTag(string fullFileName)
        {
            MD5 md5 = MD5CryptoServiceProvider.Create();
            using (FileStream fs = System.IO.File.Open(fullFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
                return System.Convert.ToBase64String(md5.ComputeHash(fs));
        }
        static private FileHistory setLocalFileEntry(string localFile, DateTime writeTime, bool isFile)
        {
            FileHistory fh = null;
            if (!config.fileStati.ContainsKey(localFile))
                config.fileStati.Add(localFile, new FileHistory()
                    {
                        localWriteTime = DateTime.MinValue,
                        remoteWriteTime = DateTime.MinValue,
                        localChanged = true
                    });

            fh = config.fileStati[localFile];
            fh.localSeen = true;
            if (fh.localWriteTime != writeTime)
            {
                //Console.WriteLine("TimeDiff: " + fh.localWriteTime.Subtract(writeTime).ToString());

                string ctag = isFile ? getLocalCTag(localFile) : null;
                fh.localWriteTime = writeTime;
                //if (fh.local_cTag != ctag || ctag == null) // lokale zeitänderung ist änderung!
                fh.localChanged = true;
                fh.local_cTag = ctag;
            }
            return fh;
        }

        static private void scanRemoteFolder(OneDriveClient odc, Item dirItem, string relRemoteFolder)
        {
            FileHistory fh;

            if (!testFilePattern(relRemoteFolder, false))
                return;

            fh = setRemoteFileEntry(relRemoteFolder, dirItem);
            addLog(!fh.remoteChanged, string.Format("r\tVerzeichnis \"{0}\" ({1}, {2})", relRemoteFolder.Substring(config.Options.RemoteBaseDir.Length - 1), fh.remoteChanged ? "1" : "0", fh.remoteWriteTime.ToString()));

            IItemChildrenCollectionPage childs = odc.Drive.Items[dirItem.Id].Children.Request().GetAsync().Result;
            while (true)
            {
                foreach (Item remoteItem in childs)
                {
                    string relFullItemName = relRemoteFolder + remoteItem.Name;
                    if (remoteItem.Folder != null)
                    {
                        relFullItemName += "/";

                        scanRemoteFolder(odc, remoteItem, relFullItemName);
                    }
                    else if (remoteItem.File != null)
                    {
                        if (!testFilePattern(relFullItemName, false))
                            continue;

                        fh = setRemoteFileEntry(relFullItemName, remoteItem);
                        addLog(!fh.remoteChanged, string.Format("r\tDatei: \"{0}\" ({1}, {2}, {3})", remoteItem.Name, fh.remoteChanged ? "1" : "0", fh.remoteWriteTime.ToString(), fh.remote_cTag));
                    }
                    else
                    {
                        if (!testFilePattern(relFullItemName, false))
                            continue;

                        bool isOneNote = remoteItem.AdditionalData != null && remoteItem.AdditionalData["package"] != null &&
                            remoteItem.AdditionalData["package"] is Newtonsoft.Json.Linq.JObject &&
                            ((Newtonsoft.Json.Linq.JObject)remoteItem.AdditionalData["package"]).GetValue("type").ToString() == "oneNote";
                        addLog(false, "HINWEIS: Datei wird übersprungen: " + relRemoteFolder + remoteItem.Name + (isOneNote ? " (OneNote Datei kann nicht gesichert werden)" : " (Inhaltstyp unbekannt)"));
                    }
                }

                if (childs.NextPageRequest != null)
                {
                    childs = childs.NextPageRequest.GetAsync().Result;
                    continue;
                }
                break;
            }
        }
        static private FileHistory setRemoteFileEntry(string relFullItemName, Item remoteItem)
        {
            FileHistory fh = null;
            DateTime writeTime = remoteItem.LastModifiedDateTime.Value.UtcDateTime;
            string localFileEquiv = remoteFileName2LocalFileName(relFullItemName);

            if (!config.fileStati.ContainsKey(localFileEquiv))
                config.fileStati.Add(localFileEquiv, new FileHistory()
                {
                    localWriteTime = DateTime.MinValue,
                    remoteWriteTime = DateTime.MinValue,
                    remoteChanged = true
                });

            fh = config.fileStati[localFileEquiv];
            fh.remoteSeen = true;
            if (fh.remoteWriteTime != writeTime)
            {
                string ctag = remoteItem.File != null ? remoteItem.CTag : null;
                fh.remoteWriteTime = writeTime;
                if (fh.remote_cTag != ctag || ctag == null) // änderung nur anerkennen, wenn tag geändert wurde
                    fh.remoteChanged = true;
                fh.remote_cTag = ctag;
            }
            return fh;
        }
    }

    public class Config
    {
        public Microsoft.OneDrive.Sdk.Authentication.AccountSession authentication;
        public SyncOptions Options;

        public Dictionary<string, FileHistory> fileStati;
    }

    public class FileHistory
    {
        public DateTime localWriteTime { get; set; }
        public string local_cTag { get; set; }

        public DateTime remoteWriteTime { get; set; }
        public string remote_cTag { get; set; }

        [IgnoreDataMember]
        public SyncDirection syncDirection { get; set; }

        [IgnoreDataMember]
        public bool localChanged { get; set; }

        [IgnoreDataMember]
        public bool remoteChanged { get; set; }

        [IgnoreDataMember]
        public bool remoteSeen { get; set; }

        [IgnoreDataMember]
        public bool localSeen { get; set; }

        [IgnoreDataMember]
        public bool SkipSync { get; set; }
    }

    public class SyncOptions
    {
        public string LogFile;
        public string RemoteBaseDir;
        public string LocalDir;
        public string FilterInclude;
        public string FilterExclude;
        public SyncMode SyncMode;
    }

    public enum SyncMode
    {
        OneDrive2Local,
        Local2OneDrive,
        BothDirections
    }

    public enum SyncDirection
    {
        Lokal2Online,
        Online2Lokal,
        NotModified
    }
}
