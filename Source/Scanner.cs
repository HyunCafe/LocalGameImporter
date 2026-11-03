// Local Game Importer 0.3.0. Read-only discovery; no process, network or database APIs.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;

namespace LocalGameImporter {
    public sealed class ExistingGame {
        public string Name, InstallDirectory;
        public string[] Paths = new string[0];
    }
    public sealed class Candidate {
        public string Path { get; set; }
        public string Relative { get; set; }
        public string Reason { get; set; }
        public int Score { get; set; }
        public long Length, Modified;
        public bool ModLoader;
        public bool LaunchWrapper;
        public bool ScriptWrapper;
        public string ContentHash;
        public override string ToString() { return Relative; }
    }
    public sealed class GameRow {
        public string ScanRoot { get; set; }
        public bool Include { get; set; }
        // Ignore selection and persistent ignored state are distinct from Add.
        // An ignored row may be displayed for restoration, never imported.
        public bool MarkIgnore { get; set; }
        public bool Ignored { get; set; }
        public bool CanMarkIgnore { get { return !IsGroup && !IsRoot && Status!="ADDED" && Status!="EXISTING"; } }
        // A visible checkbox is only editable for game candidates, never completed
        // imports, existing library entries, scan-root placeholders or utilities.
        public bool CanToggleImport { get { return !IsGroup && !IsRoot &&
            Status!="ADDED" && Status!="EXISTING" && Status!="UTILITY" && !Ignored; } }
        public string Title { get; set; }
        public string Folder { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
        public Candidate Choice { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string Tracking { get; set; }
        public bool IsGroup { get; set; }
        public bool IsRoot { get; set; }
        public string MetadataStatus { get; set; }
        public Guid ImportedId;
        public List<Candidate> Candidates { get; set; }
        public string Executable { get { return Choice == null ? "" : Choice.Relative; } }
        public bool Complete;
        public List<string> Issues = new List<string>();
    }
    public sealed class ScanRequest {
        public string Root;
        public string[] Excluded,Groups;
    }
    public sealed class ScanResult {
        public List<GameRow> Rows = new List<GameRow>();
        public string Error;
        public int Existing;
        public Dictionary<string,string> RootErrors = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    }
    public static class Scanner {
        public static string CleanTitle(string title) {
            string s = Regex.Replace(title ?? "", @"(?i)\[\s*(?:fitgirl|repack|elamigos)[^\]]*\]", " ");
            s = Regex.Replace(s, @"(?i)\s+-\s+(?:fitgirl(?:\s+repack)?|elamigos|repack)\b.*$", "");
            s = Regex.Replace(s, @"(?i)\s*\((?:fitgirl(?:\s+repack)?|elamigos|repack)\)\s*", " ");
            s = Regex.Replace(s, @"(?i)\s+v\d+(?:\.\d+){1,5}(?:\s*\+\s*DLCs?)?\s*$", "");
            // Editions/sequels and non-Latin titles remain distinct.
            return Regex.Replace(s, @"\s+", " ").Trim();
        }
        public static string Key(string title) {
            return Regex.Replace(CleanTitle(title), @"[^\p{L}\p{Nd}]", "").ToLowerInvariant();
        }
        public static string Canonical(string path) {
            if (String.IsNullOrWhiteSpace(path)) return "";
            try {
                string full=Path.GetFullPath(path);
                if(String.Equals(full,Path.GetPathRoot(full),StringComparison.OrdinalIgnoreCase))return full;
                return full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            }
            catch { return ""; }
        }
        public static bool Within(string path,string root) {
            string p=Canonical(path),r=Canonical(root);
            return p.Length>0 && r.Length>0 && p.StartsWith(r.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
        }
        public static string FolderKey(string root,string folder) {
            if(!Within(folder,root))return "";
            return Canonical(folder).Substring(Canonical(root).TrimEnd(Path.DirectorySeparatorChar).Length)
                .TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Replace('\\','/');
        }
        public static bool AllowedGameFolder(string root,string folder,string[] groups) {
            try {
                if(!ValidRoot(root) || !Within(folder,root) || !NoLinks(folder))return false;
                var marked=new HashSet<string>(groups ?? new string[0],StringComparer.OrdinalIgnoreCase);
                if(marked.Contains(FolderKey(root,folder)))return false;
                string parent=Canonical(Directory.GetParent(folder).FullName);
                int depth=0;
                while(!String.Equals(parent,Canonical(root),StringComparison.OrdinalIgnoreCase)) {
                    if(++depth>4 || !Within(parent,root) || !marked.Contains(FolderKey(root,parent)))return false;
                    parent=Canonical(Directory.GetParent(parent).FullName);
                }
                return true;
            } catch { return false; }
        }
        public static bool NoLinks(string path) {
            try {
                string p=Path.GetFullPath(path);
                while(!String.IsNullOrEmpty(p)) {
                    if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)return false;
                    p=Path.GetDirectoryName(p);
                }
                return true;
            } catch { return false; }
        }
        public static bool ValidRoot(string path) {
            try {
                if(Path.DirectorySeparatorChar=='\\' && !Regex.IsMatch(path ?? "",@"^[A-Za-z]:[\\/]"))return false;
                if(Path.DirectorySeparatorChar=='\\' && !String.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) &&
                    new DriveInfo(Path.GetPathRoot(path)).DriveType==DriveType.Network)return false;
                return !String.IsNullOrWhiteSpace(path) && !path.StartsWith(@"\\") &&
                    Path.IsPathRooted(path) && Directory.Exists(path) &&
                    Directory.GetParent(Path.GetFullPath(path))!=null && NoLinks(path);
            } catch { return false; }
        }
        public static bool ExcludedDirectory(string name) {
            return Regex.IsMatch(name,@"(?i)(?:^(?:_?backups?)(?:$|[ ._-])|_Data$)") ||
                Regex.IsMatch(name,@"(?i)^(?:_?commonredist|redist|redistributables?|support|installers?|directx|vcredist|tools|extras|prereq(?:uisites)?|crashreports?|mods|__installer|engine|\.git|archive|content|textures|meshes|audio|sound|movies|videos|screenshots|logs|cache|red4ext|redscript|BepInEx|MelonLoader|reframework|cyber_engine_tweaks|reshade-shaders)$");
        }
        public static bool ExcludedExe(string path) {
            string n=Path.GetFileNameWithoutExtension(path);
            // Whole names/prefixes with boundaries; 'eac' must not exclude 'BeachBuggyRacing'.
            return Regex.IsMatch(n,@"(?i)^(?:unins\d*|uninstall(?:er)?|setup(?:[._ -].*)?|install(?:er)?|dxsetup|vcredist.*|vc_redist.*|dotnet.*|unitycrashhandler.*|crash(?:report(?:er)?|handler).*|crashpad_handler|crashreportclient.*|reportcrash|werfault|easyanticheat.*|eac(?:[._-].*)?|autorun|verify(?:[._ -].*)?|patch(?:er)?|update(?:r)?|benchmark|config(?:urator)?|settings|rapidcrc(?:unicode)?|quicksfv|md5sum|7z(?:a|g)?|unrar|steamwebhelper|galaxyclient(?:[ _-].*)?)$");
        }
        public static bool IsScript(string path) {
            string ext=Path.GetExtension(path);
            return String.Equals(ext,".bat",StringComparison.OrdinalIgnoreCase) || String.Equals(ext,".cmd",StringComparison.OrdinalIgnoreCase);
        }
        static string HashScript(string path) {
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var hash=SHA256.Create()) {
                if(stream.Length==0 || stream.Length>1024*1024)throw new IOException("Unsupported script size");
                return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","");
            }
        }
        public static string LaunchDirectory(GameRow row) {
            try {
                string value=String.IsNullOrWhiteSpace(row.WorkingDirectory)?Path.GetDirectoryName(row.Choice.Path):row.WorkingDirectory.Trim();
                if(!Path.IsPathRooted(value))value=Path.Combine(row.Folder,value);
                string path=Canonical(value);
                if(!Directory.Exists(path) || !NoLinks(path) ||
                    !(String.Equals(path,Canonical(row.Folder),StringComparison.OrdinalIgnoreCase) || Within(path,row.Folder)))return null;
                return path;
            } catch { return null; }
        }
        static void Incomplete(GameRow row,string reason) {
            row.Complete=false;
            if(!row.Issues.Contains(reason))row.Issues.Add(reason);
        }
        public static Candidate ManualCandidate(string root,GameRow row,string path,string[] groups) {
            if(row==null || row.IsGroup || row.IsRoot || !AllowedGameFolder(root,row.Folder,groups) ||
                !Within(path,row.Folder) || !NoLinks(path) || ExcludedExe(path))return null;
            try {
                if(!IsScript(path) && !(String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase) && PortableExecutable(path)))return null;
                return Describe(new FileInfo(path),row);
            } catch { return null; }
        }
        static bool UtilityFolder(string title) {
            string key=Key(title);
            return key=="goggalaxy" || key=="ubisoftgamelauncher" || key=="epicgameslauncher" || key=="eadesktop" || key=="vortex";
        }
        public static string Duplicate(GameRow row,IEnumerable<ExistingGame> existing) {
            foreach(var g in existing) {
                string dir=Canonical(g.InstallDirectory);
                if(dir.Length>0 && (String.Equals(dir,Canonical(row.Folder),StringComparison.OrdinalIgnoreCase) || Within(dir,row.Folder)))return "PATH";
                foreach(string p in g.Paths ?? new string[0])
                    if(!String.IsNullOrEmpty(p) && Path.IsPathRooted(p) && Within(p,row.Folder))return "PATH";
            }
            string k=Key(row.Title);
            if(k.Length>0 && existing.Any(g=>Key(g.Name)==k))return "TITLE";
            return "";
        }
        static bool KnownMatch(string title,string exe) {
            string t=Key(title),e=Key(exe);
            return (t.StartsWith("cyberpunk2077") && e=="cyberpunk2077") ||
                (t.Contains("witcher3") && e=="witcher3") ||
                (t.Contains("skyrim") && (e=="skyrimse" || e=="skyrim")) ||
                (t.StartsWith("baldursgate3") && (e=="bg3" || e=="bg3dx11")) ||
                (t.StartsWith("reddeadredemption2") && e=="rdr2") ||
                (t.StartsWith("grandtheftautov") && e=="gta5") ||
                (t.StartsWith("fallout4") && e=="fallout4");
        }
        static bool PortableExecutable(string path) {
            // Parse signatures only. Reading bytes does not execute the target.
            using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var r=new BinaryReader(f)) {
                if(f.Length<64 || r.ReadUInt16()!=0x5A4D)return false;
                f.Position=0x3C;int offset=r.ReadInt32();
                if(offset<64 || offset>f.Length-24)return false;
                f.Position=offset;if(r.ReadUInt32()!=0x4550)return false;
                f.Position=offset+22;ushort flags=r.ReadUInt16();
                return (flags&0x0002)!=0 && (flags&0x2000)==0;
            }
        }
        static Candidate Describe(FileInfo f,GameRow row) {
            string exe=Path.GetFileNameWithoutExtension(f.Name),title=Key(row.Title),key=Key(exe);
            int score=0;string reason="Filename needs a decision";
            bool loader=Regex.IsMatch(exe,@"(?i)^(?:skse(?:64|vr)?_loader|f4se(?:vr)?_loader|sfse_loader|x?nvse_loader|[ox]?obse_loader|fose_loader|StardewModdingAPI|ModOrganizer|Vortex|r2modman|Thunderstore\s*Mod\s*Manager)$");
            bool script=IsScript(f.FullName);
            bool wrapper=script || Regex.IsMatch(exe,@"(?i)(?:launcher$|^launch(?:er|game)?(?:[._ -]|$)|^start(?:[._ -]|game$|$)|^play(?:game)?$)");
            if(key.Length>=3 && key==title) { score=200;reason="Exact title/executable name"; }
            else if(KnownMatch(row.Title,exe)) { score=190;reason="Recognized title/executable pair"; }
            else if(key.Length>=4 && title.Contains(key)) { score=110;reason="Partial title match; review"; }
            else if(title.Length>=5 && key.Contains(title)) { score=100;reason="Partial title match; review"; }
            if(!script && score<190 && title.Length>=4) {
                try {
                    var info=FileVersionInfo.GetVersionInfo(f.FullName);
                    if(Key(info.ProductName)==title || Key(info.FileDescription)==title) { score=190;reason="Embedded product title matches folder"; }
                } catch { /* Metadata is optional; no executable is launched. */ }
            }
            if(loader) { score=150;reason="Mod loader: choose the intended launch method"; }
            if(script) { score=0;reason="Command wrapper; manual selection only. Contents are not executed or safety-checked."; }
            return new Candidate { Path=f.FullName,Relative=f.FullName.Substring(row.Folder.Length).TrimStart(Path.DirectorySeparatorChar),
                Score=score,Reason=reason,Length=f.Length,Modified=f.LastWriteTimeUtc.Ticks,ModLoader=loader,LaunchWrapper=wrapper,
                ScriptWrapper=script,ContentHash=script?HashScript(f.FullName):null };
        }
        public static bool Revalidate(string root,GameRow row,Candidate candidate,string[] groups=null) {
            try {
                if(!AllowedGameFolder(root,row.Folder,groups))return false;
                if(candidate==null || !Within(candidate.Path,row.Folder) || !NoLinks(candidate.Path) || ExcludedExe(candidate.Path))return false;
                var f=new FileInfo(candidate.Path);
                return f.Exists && f.Length==candidate.Length && f.LastWriteTimeUtc.Ticks==candidate.Modified &&
                    (candidate.ScriptWrapper?(IsScript(f.FullName) && HashScript(f.FullName)==candidate.ContentHash):
                    (String.Equals(f.Extension,".exe",StringComparison.OrdinalIgnoreCase) && PortableExecutable(f.FullName)));
            } catch { return false; }
        }
        static IEnumerable<string> GameFolders(string root,HashSet<string> groups,ScanResult result,CancellationToken cancel) {
            var pending=new Queue<KeyValuePair<string,int>>();pending.Enqueue(new KeyValuePair<string,int>(root,0));
            int visited=0;
            while(pending.Count>0) {
                cancel.ThrowIfCancellationRequested();var parent=pending.Dequeue();
                if(!NoLinks(parent.Key))throw new IOException("Category became inaccessible or linked");
                foreach(string folder in Directory.EnumerateDirectories(parent.Key).Take(2001).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)) {
                    cancel.ThrowIfCancellationRequested();
                    if(++visited>2000) { result.Error="Folder limit reached. Automatic import was skipped.";yield break; }
                    if(ExcludedDirectory(Path.GetFileName(folder)))continue;
                    if(groups.Contains(FolderKey(root,folder))) {
                        bool complete=NoLinks(folder) && parent.Value<4;
                        result.Rows.Add(new GameRow { ScanRoot=Canonical(root),Folder=folder,Title=Path.GetFileName(folder),IsGroup=true,Status=complete?"GROUP":"BLOCKED",
                            Reason=complete?"Category folder; its children are scanned as games":"Linked, inaccessible or too deeply grouped folder",
                            Candidates=new List<Candidate>(),Complete=complete });
                        if(complete)pending.Enqueue(new KeyValuePair<string,int>(folder,parent.Value+1));
                    } else yield return folder;
                }
            }
        }
        public static ScanResult Scan(string root,ExistingGame[] existing,string[] ignored,CancellationToken cancel,double stableSeconds=60,string[] groups=null,double budgetSeconds=120) {
            var result=new ScanResult();
            if(!ValidRoot(root)) { result.Error="Configured folder is unavailable, is a drive root, or uses a link. No entries were changed.";return result; }
            var excluded=new HashSet<string>(ignored ?? new string[0],StringComparer.OrdinalIgnoreCase);
            var marked=new HashSet<string>(groups ?? new string[0],StringComparer.OrdinalIgnoreCase);
            var started=DateTime.UtcNow;
            try {
                int folders=0;
                foreach(string folder in GameFolders(root,marked,result,cancel)) {
                    cancel.ThrowIfCancellationRequested();
                    if(++folders>2000 || (DateTime.UtcNow-started).TotalSeconds>budgetSeconds) { result.Error="Folder/time limit reached. Automatic import was skipped for this scan folder.";break; }
                    var row=new GameRow { ScanRoot=Canonical(root),Folder=Path.GetFullPath(folder),Title=CleanTitle(Path.GetFileName(folder)),Candidates=new List<Candidate>(),Complete=true,Status="REVIEW",Tracking="Default" };
                    if(ExcludedDirectory(Path.GetFileName(folder)))continue;
                    if(!NoLinks(folder)) { row.Status="BLOCKED";row.Reason="Linked or inaccessible folder";result.Rows.Add(row);continue; }
                    string duplicate=Duplicate(row,existing);
                    if(duplicate=="PATH") { result.Existing++;row.Status="EXISTING";row.Reason="Existing Playnite folder/action preserved";result.Rows.Add(row);continue; }
                    if(UtilityFolder(row.Title)) { row.Status="UTILITY";row.Reason="Store/mod-manager folder; add its actual game directories as scan folders or mark it as a category";result.Rows.Add(row);continue; }
                    var queue=new Queue<KeyValuePair<string,int>>();queue.Enqueue(new KeyValuePair<string,int>(folder,0));
                    int dirs=0,files=0;bool recent=false,modded=false;
                    while(queue.Count>0) {
                        cancel.ThrowIfCancellationRequested();
                        if(++dirs>500) { Incomplete(row,"500-folder limit");break; }
                        if((DateTime.UtcNow-started).TotalSeconds>budgetSeconds) { Incomplete(row,"scan time limit");break; }
                        var step=queue.Dequeue();
                        if(!NoLinks(step.Key)) { Incomplete(row,"linked or inaccessible subfolder");continue; }
                        try {
                            if(Directory.EnumerateFiles(step.Key,"vortex.deployment.*",SearchOption.TopDirectoryOnly).Any())modded=true;
                            foreach(string marker in new [] { "mod.settings","mods.settings","doorstop_config.ini","dinput8.dll","winmm.dll","version.dll","dxgi.dll" })
                                if(File.Exists(Path.Combine(step.Key,marker)))modded=true;
                            var launches=Directory.EnumerateFiles(step.Key,"*.exe",SearchOption.TopDirectoryOnly)
                                .Concat(Directory.EnumerateFiles(step.Key,"*.bat",SearchOption.TopDirectoryOnly))
                                .Concat(Directory.EnumerateFiles(step.Key,"*.cmd",SearchOption.TopDirectoryOnly));
                            foreach(string file in launches) {
                                cancel.ThrowIfCancellationRequested();
                                if((DateTime.UtcNow-started).TotalSeconds>budgetSeconds) { Incomplete(row,"scan time limit");break; }
                                if(++files>10000) { Incomplete(row,"10000 launch-file limit");break; }
                                if(ExcludedExe(file))continue;
                                if(row.Candidates.Count>=200) { Incomplete(row,"200-candidate limit");break; }
                                if(!NoLinks(file)) { Incomplete(row,"linked or inaccessible launch file");continue; }
                                try {
                                    if(!IsScript(file) && !PortableExecutable(file))continue;
                                    var f=new FileInfo(file);var candidate=Describe(f,row);row.Candidates.Add(candidate);
                                    if((DateTime.UtcNow-f.LastWriteTimeUtc).TotalSeconds<stableSeconds || (DateTime.UtcNow-f.CreationTimeUtc).TotalSeconds<stableSeconds)recent=true;
                                } catch(UnauthorizedAccessException) { Incomplete(row,"access denied reading launch file"); }
                                catch(PathTooLongException) { Incomplete(row,"path too long reading launch file"); }
                                catch(IOException) { Incomplete(row,"locked/unreadable launch file or unsupported script size"); }
                                catch { Incomplete(row,"launch-file read failed"); }
                            }
                            foreach(string sub in Directory.EnumerateDirectories(step.Key)) {
                                cancel.ThrowIfCancellationRequested();
                                string subName=Path.GetFileName(sub);
                                if(Regex.IsMatch(subName,@"(?i)^(?:mods|red4ext|skse|f4se|nvse|sfse|obse|plugins|BepInEx|MelonLoader|reframework|cyber_engine_tweaks|reshade-shaders)$")) {
                                    modded=true;
                                }
                                if(ExcludedDirectory(Path.GetFileName(sub)))continue;
                                if(queue.Count+dirs>=500) { Incomplete(row,"500-folder limit");break; }
                                if(step.Value>=6) { Incomplete(row,"6-level launch-search depth limit");continue; }
                                queue.Enqueue(new KeyValuePair<string,int>(sub,step.Value+1));
                            }
                        } catch(OperationCanceledException) { throw; }
                        catch(UnauthorizedAccessException) { Incomplete(row,"access denied listing subfolder"); }
                        catch(PathTooLongException) { Incomplete(row,"path too long listing subfolder"); }
                        catch(IOException) { Incomplete(row,"unreadable subfolder"); }
                        catch { Incomplete(row,"subfolder enumeration failed"); }
                    }
                    row.Candidates=row.Candidates.OrderByDescending(c=>c.Score).ThenBy(c=>c.Relative,StringComparer.OrdinalIgnoreCase).ToList();
                    row.Choice=row.Candidates.FirstOrDefault();
                    if(row.Choice==null) { row.Status="NO MATCH";row.Reason="No supported executable found"; }
                    else {
                        int second=row.Candidates.Count>1?row.Candidates[1].Score:0;
                        bool loader=row.Candidates.Any(c=>c.ModLoader),wrapper=row.Candidates.Any(c=>c.LaunchWrapper);
                        if(row.Choice.Score>=190 && row.Choice.Score-second>=30 && !loader && !wrapper && !modded) { row.Status="READY";row.Include=true; }
                        row.Reason=(loader || wrapper || modded)?"Launcher/mod setup detected; confirm executable and launch arguments":row.Choice.Reason;
                        if(row.Candidates.Count>1 && row.Choice.Score-second<30)row.Reason="Several plausible executables; choose one below";
                    }
                    if(!row.Complete) { row.Status="INCOMPLETE";row.Reason=String.Join("; ",row.Issues)+". Choose a known launcher manually or resolve and rescan.";row.Include=false; }
                    else if(recent && row.Status=="READY") { row.Status="WAIT";row.Reason="Executable recently created/changed; rescan after installation finishes";row.Include=false; }
                    if(duplicate=="TITLE") { if(row.Complete)row.Status="NAME MATCH";row.Reason="A library entry has this title; check before adding another. "+row.Reason;row.Include=false; }
                    if(excluded.Contains(FolderKey(root,folder))) { row.Status="EXCLUDED";row.Reason="Excluded from automatic import; select a candidate to include again";row.Include=false; }
                    result.Rows.Add(row);
                }
                // Two new folders normalizing to the same title also need a decision.
                foreach(var group in result.Rows.Where(r=>!r.IsGroup && r.Status!="EXISTING").GroupBy(r=>Key(r.Title)).Where(g=>g.Count()>1))
                    foreach(var row in group) { if(row.Complete)row.Status="NAME MATCH";row.Reason="Multiple folders share this title; review. "+row.Reason;row.Include=false; }
            } catch(OperationCanceledException) { result.Error="Scan cancelled. Automatic import was skipped."; }
            catch { result.Error="Part of the root could not be enumerated. Automatic import was skipped."; }
            return result;
        }
        public static ScanResult ScanMany(ScanRequest[] requests,ExistingGame[] existing,CancellationToken cancel,double stableSeconds=60) {
            var combined=new ScanResult();var started=DateTime.UtcNow;
            foreach(var request in requests) {
                if(cancel.IsCancellationRequested) { combined.Error="Scan cancelled. Automatic import was skipped.";return combined; }
                string root=Canonical(request.Root);
                double remaining=180-(DateTime.UtcNow-started).TotalSeconds;
                var result=remaining>0?Scan(request.Root,existing,request.Excluded,cancel,stableSeconds,request.Groups,Math.Min(120,remaining)):
                    new ScanResult { Error="Combined 180-second scan budget reached; rescan this folder separately." };
                combined.Rows.AddRange(result.Rows);combined.Existing+=result.Existing;
                if(result.Error!=null) {
                    combined.RootErrors[root]=result.Error;
                    combined.Rows.Add(new GameRow { ScanRoot=root,Folder=request.Root,Title=request.Root,Status="ROOT ERROR",Reason=result.Error,IsRoot=true,Candidates=new List<Candidate>() });
                }
                else if(result.Rows.Count==0 && Directory.Exists(request.Root)) {
                    combined.Rows.Add(new GameRow { ScanRoot=root,Folder=request.Root,Title=request.Root,Status="NO GAME FOLDERS",Reason="No supported game folders found. Loose ROM files use Playnite's emulator scanner; EXE games must have their own subfolders.",IsRoot=true,Candidates=new List<Candidate>() });
                }
            }
            foreach(var group in combined.Rows.Where(r=>!r.IsGroup && !r.IsRoot && r.Status!="EXISTING").GroupBy(r=>Key(r.Title)).Where(g=>g.Count()>1))
                foreach(var row in group) { if(row.Complete)row.Status="NAME MATCH";row.Reason="Same title in multiple folders; review before adding. "+row.Reason;row.Include=false; }
            return combined;
        }
    }
}
