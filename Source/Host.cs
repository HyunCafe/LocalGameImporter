// Playnite-facing adapter. Only UI/database work runs on Playnite's UI dispatcher.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Playnite.SDK;
using Playnite.SDK.Models;

namespace LocalGameImporter {
    [DataContract]
    public sealed class FolderPreferences {
        [DataMember] public string Root;
        [DataMember] public string ReviewedRoot = "";
        [DataMember] public List<string> Excluded = new List<string>();
        [DataMember] public List<string> GroupedFolders = new List<string>();
        public override string ToString() { return Root; }
    }
    [DataContract]
    public sealed class Preferences {
        // Legacy fields are read only to migrate existing local settings.
        [DataMember(EmitDefaultValue=false)] public string Root;
        [DataMember] public bool Automatic = true;
        [DataMember(EmitDefaultValue=false)] public string ReviewedRoot;
        [DataMember(EmitDefaultValue=false)] public List<string> Excluded;
        [DataMember(EmitDefaultValue=false)] public List<string> GroupedFolders;
        [DataMember] public List<FolderPreferences> Folders;
        [DataMember] public int ReviewPolicyVersion = 3;
    }
    public static class Host {
        static IPlayniteAPI api;
        static string dataPath;
        static Preferences prefs;
        static CancellationTokenSource cancellation;
        static bool busy,wantReview;
        static volatile bool stopping;
        static DateTime lastStart=DateTime.MinValue;
        static ScanResult latest;
        static Window window;
        static readonly List<Guid> sessionAdded=new List<Guid>();
        const string Notice="LocalGameImporter.Scan";

        public static void Initialize(IPlayniteAPI value,string directory) {
            if(api!=null)return;
            api=value;dataPath=directory;prefs=new Preferences();sessionAdded.Clear();
            try {
                string file=Path.Combine(dataPath,"settings.json");
                if(File.Exists(file)) {
                    if(!Scanner.NoLinks(file) || new FileInfo(file).Length>1024*1024)throw new IOException();
                    using(var f=File.OpenRead(file))prefs=(Preferences)new DataContractJsonSerializer(typeof(Preferences)).ReadObject(f);
                }
                NormalizePreferences();
            } catch {
                prefs=new Preferences { Automatic=false };
                NormalizePreferences();
                Notify("Local Games settings could not be read. Automatic import is off; review settings before saving.",true);
            }
        }
        static void NormalizePreferences() {
            if(prefs==null)throw new IOException("Missing preferences");
            if(prefs.Folders==null) {
                prefs.Folders=new List<FolderPreferences>();
                if(!String.IsNullOrWhiteSpace(prefs.Root))prefs.Folders.Add(new FolderPreferences {
                    Root=prefs.Root,ReviewedRoot=prefs.ReviewedRoot ?? "",Excluded=prefs.Excluded ?? new List<string>(),GroupedFolders=prefs.GroupedFolders ?? new List<string>()
                });
            }
            if(prefs.Folders.Count>16)throw new IOException("Invalid folder count");
            var seen=new List<string>();
            foreach(var folder in prefs.Folders) {
                if(folder==null || String.IsNullOrWhiteSpace(folder.Root) || !Path.IsPathRooted(folder.Root))throw new IOException("Invalid scan folder");
                string root=Scanner.Canonical(folder.Root);
                if(seen.Any(p=>String.Equals(p,root,StringComparison.OrdinalIgnoreCase) || Scanner.Within(p,root) || Scanner.Within(root,p)))throw new IOException("Overlapping scan folders");
                seen.Add(root);
                if(folder.Excluded==null)folder.Excluded=new List<string>();
                if(folder.GroupedFolders==null)folder.GroupedFolders=new List<string>();
                if(prefs.ReviewPolicyVersion!=3)folder.ReviewedRoot="";
            }
            prefs.ReviewPolicyVersion=3;prefs.Root=null;prefs.ReviewedRoot=null;prefs.Excluded=null;prefs.GroupedFolders=null;
        }
        static FolderPreferences FolderFor(string root) {
            return prefs.Folders.FirstOrDefault(f=>String.Equals(Scanner.Canonical(f.Root),Scanner.Canonical(root),StringComparison.OrdinalIgnoreCase));
        }
        static void Save() {
            try { SaveCore(); }
            catch { prefs.Automatic=false;throw; }
        }
        static void SaveCore() {
            if(String.IsNullOrEmpty(dataPath))throw new IOException("Extension data folder unavailable");
            Directory.CreateDirectory(dataPath);
            if(!Scanner.NoLinks(dataPath))throw new IOException("Linked settings folder");
            string path=Path.Combine(dataPath,"settings.json"),temp=Path.Combine(dataPath,"settings-"+Guid.NewGuid().ToString("N")+".tmp");
            if(File.Exists(path) && !Scanner.NoLinks(path))throw new IOException("Linked settings file");
            try {
                using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    new DataContractJsonSerializer(typeof(Preferences)).WriteObject(f,prefs);f.Flush(true);
                }
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            }
            finally { if(File.Exists(temp) && Scanner.NoLinks(temp))File.Delete(temp); }
        }
        static void Notify(string text,bool warning) {
            if(stopping)return;
            api.Notifications.Add(new NotificationMessage(Notice,text,warning?NotificationType.Error:NotificationType.Info,()=>Review()));
        }
        static string ExpandAction(string path,Game game) {
            if(String.IsNullOrWhiteSpace(path))return "";
            string p=path.Replace("{InstallDir}",game.InstallDirectory ?? "");
            p=Environment.ExpandEnvironmentVariables(p);
            if(p.Contains("{") || p.Contains("}"))return "";
            if(!Path.IsPathRooted(p) && !String.IsNullOrEmpty(game.InstallDirectory))p=Path.Combine(game.InstallDirectory,p);
            return p;
        }
        static ExistingGame[] Existing() {
            return api.Database.Games.Select(g=>new ExistingGame {
                Name=g.Name,InstallDirectory=g.InstallDirectory,
                Paths=g.GameActions==null?new string[0]:g.GameActions.Where(a=>a!=null && a.Type==GameActionType.File).Select(a=>ExpandAction(a.Path,g)).ToArray()
            }).ToArray();
        }
        static bool Dispatch(Action action) {
            if(api.MainView.UIDispatcher.CheckAccess())return false;
            if(!api.MainView.UIDispatcher.HasShutdownStarted)api.MainView.UIDispatcher.BeginInvoke(action);
            return true;
        }
        public static void StartupOrRefresh() { if(!Dispatch(StartupOrRefresh))Scan(false); }
        public static void Review() {
            if(Dispatch(Review))return;
            if(stopping)return;
            if(window!=null) { window.Activate();return; }
            if(prefs.Folders.Count==0) { ShowFolders();return; }
            if(latest!=null && !busy) { ShowReview();return; }
            wantReview=true;Scan(true);
        }
        public static void Rescan() { if(Dispatch(Rescan))return;wantReview=true;Scan(true); }
        public static void Cancel() { var c=cancellation;if(c!=null) { try { c.Cancel(); } catch(ObjectDisposedException) {} } }
        public static void Stop() {
            stopping=true;Cancel();
        }
        public static bool AutomaticEnabled() {
            return prefs!=null && prefs.Automatic && prefs.Folders.Any(f=>String.Equals(Scanner.Canonical(f.Root),f.ReviewedRoot,StringComparison.OrdinalIgnoreCase));
        }
        public static bool AutomaticFor(string root) {
            var f=FolderFor(root);
            return prefs.Automatic && f!=null && String.Equals(Scanner.Canonical(f.Root),f.ReviewedRoot,StringComparison.OrdinalIgnoreCase);
        }
        public static string Summary(ScanResult result) {
            if(result==null)return "No scan completed yet.";
            int added=result.Rows.Count(r=>r.Status=="ADDED"),existing=result.Rows.Count(r=>r.Status=="EXISTING"),
                excluded=result.Rows.Count(r=>r.Status=="EXCLUDED"),ready=result.Rows.Count(r=>r.Status=="READY"),
                attention=result.Rows.Count(r=>!r.IsGroup && r.Status!="ADDED" && r.Status!="EXISTING" && r.Status!="EXCLUDED" && r.Status!="READY"),
                blockedGroups=result.Rows.Count(r=>r.IsGroup && r.Status!="GROUP");
            return "Added: "+added+" | Existing: "+existing+" | Excluded: "+excluded+" | Ready: "+ready+" | Need attention: "+(attention+blockedGroups);
        }
        public static List<Guid> ImportedGames() {
            return sessionAdded.Where(id=>api.Database.Games.Get(id)!=null).Distinct().ToList();
        }
        public static string MetadataState(Game game) {
            if(game==null)return "Entry unavailable";
            var missing=new List<string>();
            if(String.IsNullOrWhiteSpace(game.CoverImage))missing.Add("cover");
            if(String.IsNullOrWhiteSpace(game.BackgroundImage))missing.Add("background");
            if(String.IsNullOrWhiteSpace(game.Description))missing.Add("description");
            if(String.IsNullOrWhiteSpace(game.Icon))missing.Add("icon");
            return missing.Count==0?"Core fields present; provider match not verified":"Missing: "+String.Join(", ",missing)+"; provider review pending";
        }
        static void RefreshMetadataStatus() {
            if(latest==null)return;
            foreach(var row in latest.Rows) {
                if(row.ImportedId!=Guid.Empty)row.MetadataStatus=MetadataState(api.Database.Games.Get(row.ImportedId));
                else if(!row.IsGroup && !row.IsRoot)row.MetadataStatus=row.Status=="EXISTING"?"Existing entry preserved":"Provider match not checked; confirm title/edition";
            }
        }
        static void Scan(bool manual) {
            if(stopping || busy)return;
            if(prefs.Folders.Count==0) { wantReview=false;Notify("Local Games: choose your game folders in Review games and automation to begin.",false);return; }
            if(!manual && ((DateTime.UtcNow-lastStart).TotalSeconds<15 || window!=null))return;
            ExistingGame[] existing;
            try { existing=Existing(); }
            catch { Notify("Local Games could not read the current library. No import was attempted.",true);return; }
            busy=true;lastStart=DateTime.UtcNow;
            var requests=prefs.Folders.Select(f=>new ScanRequest { Root=f.Root,Excluded=f.Excluded.ToArray(),Groups=f.GroupedFolders.ToArray() }).ToArray();
            cancellation=new CancellationTokenSource();var token=cancellation.Token;
            Task.Factory.StartNew(()=>Scanner.ScanMany(requests,existing,token),token,TaskCreationOptions.None,TaskScheduler.Default).ContinueWith(task=> {
                if(stopping || api.MainView.UIDispatcher.HasShutdownStarted)return;
                api.MainView.UIDispatcher.BeginInvoke(new Action(()=> {
                    busy=false;
                    if(cancellation!=null) { cancellation.Dispose();cancellation=null; }
                    if(stopping)return;
                    latest=(task.IsCanceled || token.IsCancellationRequested)?new ScanResult { Error="Scan cancelled. No import was attempted." }:
                        task.IsFaulted?new ScanResult { Error="Scan failed. No import was attempted." }:task.Result;
                    if(task.IsFaulted)task.Exception.Handle(e=>true);
                    bool show=manual || wantReview;wantReview=false;
                    if(!show && latest.Error==null && AutomaticEnabled())Import(latest.Rows.Where(r=>r.Status=="READY" && !latest.RootErrors.ContainsKey(r.ScanRoot)).ToList(),true);
                    int review=latest.Rows.Count(r=>!r.IsGroup && r.Status!="ADDED" && r.Status!="EXCLUDED" && r.Status!="EXISTING");
                    if(latest.Error!=null)Notify(latest.Error,true);
                    else if(review>0 || latest.RootErrors.Count>0)Notify("Local Games — "+Summary(latest)+". Click to inspect.",latest.RootErrors.Count>0);
                    else if(!latest.Rows.Any(r=>r.Status=="ADDED"))api.Notifications.Remove(Notice);
                    if(show)ShowReview();
                }));
            });
        }
        static void Import(List<GameRow> rows,bool automatic) {
            int added=0,failed=0;var batchIds=new List<Guid>();
            foreach(var row in rows) {
                if(stopping)break;
                try {
                    if(row.IsGroup || row.IsRoot || row.Choice==null)continue;
                    var folder=FolderFor(row.ScanRoot);
                    if(folder==null) { row.Status="CHANGED";row.Reason="Scan folder is no longer configured";row.Include=false;failed++;continue; }
                    if(automatic && (!AutomaticFor(row.ScanRoot) || row.Status!="READY" || !row.Complete || row.Choice.ScriptWrapper || row.Choice.ModLoader || row.Choice.LaunchWrapper))continue;
                    string duplicate=Scanner.Duplicate(row,Existing());
                    if(duplicate=="PATH" || (automatic && duplicate=="TITLE")) { row.Include=false;row.Status="EXISTING";row.Reason="Already represented in the library";continue; }
                    if(!Scanner.Revalidate(folder.Root,row,row.Choice,folder.GroupedFolders.ToArray())) { row.Include=false;row.Status="CHANGED";row.Reason="File changed or path became unavailable; rescan";failed++;continue; }
                    string working=Scanner.LaunchDirectory(row);
                    if(working==null || (!String.IsNullOrEmpty(row.Tracking) && row.Tracking!="Default" && row.Tracking!="Process" && row.Tracking!="Folder")) {
                        row.Include=false;row.Status="REVIEW";row.Reason="Choose a working directory inside this game folder and a supported tracking mode";failed++;continue;
                    }
                    if(String.IsNullOrWhiteSpace(row.Title)) { row.Status="REVIEW";row.Reason="Enter a title";failed++;continue; }
                    var game=new Game { Name=row.Title.Trim(),InstallDirectory=row.Folder,IsInstalled=true,
                        GameActions=new ObservableCollection<GameAction> {
                            new GameAction { Name="Play",Type=GameActionType.File,IsPlayAction=true,Path=row.Choice.Path,
                                WorkingDir=working,TrackingMode=row.Tracking=="Folder"?TrackingMode.Directory:row.Tracking=="Process"?TrackingMode.Process:TrackingMode.Default,
                                TrackingPath=row.Tracking=="Folder"?row.Folder:null,InitialTrackingDelay=row.Tracking=="Folder"?2000:0,Arguments=row.Arguments ?? "" }
                        }
                    };
                    var pc=api.Database.Platforms.FirstOrDefault(p=>String.Equals(p.Name,"PC (Windows)",StringComparison.OrdinalIgnoreCase));
                    if(pc!=null)game.PlatformIds=new List<Guid> { pc.Id };
                    // Never update/remove an existing entry, run a game or set global launch scripts.
                    try { api.Database.Games.Add(game); }
                    catch { if(api.Database.Games.Get(game.Id)==null)throw; }
                    if(api.Database.Games.Get(game.Id)==null)throw new IOException("Library add not confirmed");
                    row.ImportedId=game.Id;if(!sessionAdded.Contains(game.Id))sessionAdded.Add(game.Id);
                    batchIds.Add(game.Id);row.MetadataStatus=MetadataState(api.Database.Games.Get(game.Id));
                    row.Status="ADDED";row.Reason="Library entry verified; launch and save-backup behavior still need a first-use check";row.Include=false;added++;
                } catch { row.Status="ERROR";row.Reason="Import failed; other entries were retained";row.Include=false;failed++; }
            }
            bool selectionFailed=false;
            if(batchIds.Count>0) {
                try { api.MainView.SelectGames(batchIds); }
                catch { selectionFailed=true; }
            }
            Notify("Local Games: added "+added+" game(s); "+failed+" failed or changed. Existing entries and game files were preserved.",failed>0);
            if(selectionFailed)Notify("Games were imported, but selection for metadata could not be updated. Open the review window and use Select imports for metadata.",true);
        }
        static void RememberUnchecked() {
            if(latest==null)return;
            foreach(var row in latest.Rows.Where(r=>r.Status=="READY" && !r.Include)) {
                var folder=FolderFor(row.ScanRoot);if(folder==null)continue;
                string key=Scanner.FolderKey(folder.Root,row.Folder);
                if(!folder.Excluded.Contains(key))folder.Excluded.Add(key);
                row.Status="EXCLUDED";row.Reason="Excluded from automatic import";
            }
        }
        static Button Button(string title,Action action) {
            var b=new Button { Content=title,Margin=new Thickness(4),Padding=new Thickness(10,5,10,5) };
            b.Click+=(s,e)=> { try { action(); } catch { api.Dialogs.ShowErrorMessage("Operation could not finish. Existing game files and entries were preserved.","Local Games"); } };
            return b;
        }
        static TextBlock Text(string value) {
            var text=new TextBlock { Text=value,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(5) };
            text.SetResourceReference(TextBlock.ForegroundProperty,"TextBrush");return text;
        }
        static void ShowFolders() {
            var dialog=api.Dialogs.CreateWindow(new WindowCreationOptions());dialog.Title="Local Games — scan folders";
            window=dialog;dialog.Closed+=(s,e)=>window=null;
            dialog.Width=760;dialog.Height=420;dialog.Owner=api.Dialogs.GetCurrentAppWindow();dialog.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            var layout=new DockPanel { Margin=new Thickness(12) };dialog.Content=layout;
            var hint=Text("All listed folders are scanned on startup and library refresh. New folders wait for an initial import review. Removing a folder here preserves its games and Playnite entries.");
            DockPanel.SetDock(hint,Dock.Top);layout.Children.Add(hint);
            var controls=new WrapPanel();DockPanel.SetDock(controls,Dock.Bottom);layout.Children.Add(controls);
            var list=new ListBox { ItemsSource=prefs.Folders,Margin=new Thickness(5) };layout.Children.Add(list);
            controls.Children.Add(Button("Add folder",()=> {
                if(prefs.Folders.Count>=16) { api.Dialogs.ShowMessage("Up to 16 scan folders are supported.","Local Games");return; }
                string root=api.Dialogs.SelectFolder();if(String.IsNullOrEmpty(root))return;
                if(!Scanner.ValidRoot(root)) { api.Dialogs.ShowErrorMessage("Choose an accessible local folder, not a whole drive or a linked folder.","Local Games");return; }
                if(prefs.Folders.Any(f=>String.Equals(Scanner.Canonical(f.Root),Scanner.Canonical(root),StringComparison.OrdinalIgnoreCase) || Scanner.Within(root,f.Root) || Scanner.Within(f.Root,root))) {
                    api.Dialogs.ShowMessage("This overlaps a configured scan folder. For a nested category, use Mark / unmark category folder in the review window.","Local Games");return;
                }
                prefs.Folders.Add(new FolderPreferences { Root=Scanner.Canonical(root) });Save();latest=null;list.Items.Refresh();
            }));
            controls.Children.Add(Button("Remove selected folder",()=> {
                var folder=list.SelectedItem as FolderPreferences;if(folder==null)return;
                if(prefs.Folders.Count==1) { api.Dialogs.ShowMessage("Add another folder before removing the last one.","Local Games");return; }
                prefs.Folders.Remove(folder);Save();latest=null;list.Items.Refresh();
            }));
            controls.Children.Add(Button("Done and rescan",()=>dialog.Close()));
            dialog.ShowDialog();Rescan();
        }
        static void ShowReview() {
            if(window!=null) { window.Activate();return; }
            if(latest==null)return;
            RefreshMetadataStatus();
            var w=api.Dialogs.CreateWindow(new WindowCreationOptions());window=w;
            var owner=api.Dialogs.GetCurrentAppWindow();if(owner!=w)w.Owner=owner;
            w.WindowStartupLocation=WindowStartupLocation.CenterOwner;
            w.Title="Local Games - review and automation";w.Width=1320;w.Height=760;w.MinWidth=900;w.MinHeight=500;
            var panel=new DockPanel { Margin=new Thickness(12) };w.Content=panel;
            var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);panel.Children.Add(top);
            top.Children.Add(Text(prefs.Folders.Count+" scan folder(s). Use Scan folders to add other local game directories."));
            var summary=Text(Summary(latest));top.Children.Add(summary);
            top.Children.Add(Text(latest.Error ?? "Check the proposed title and executable. READY rows are selected. For an ambiguous row, choose its executable below."));
            var auto=new CheckBox { Content="Automatically add future clear matches on startup / library refresh",IsChecked=prefs.Automatic,Margin=new Thickness(5) };
            top.Children.Add(auto);
            top.Children.Add(Text("Automation starts after this first review. Unchecked READY rows stay excluded from automation. Changes to this checkbox are saved with Import checked / Save preferences."));
            var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);panel.Children.Add(bottom);
            var grid=new DataGrid { AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,SelectionMode=DataGridSelectionMode.Single,FrozenColumnCount=3,
                ItemsSource=latest.Rows,Margin=new Thickness(4),IsReadOnly=false };
            grid.Columns.Add(new DataGridCheckBoxColumn { Header="Add",Binding=new Binding("Include") { Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged },Width=45 });
            grid.Columns.Add(new DataGridTextColumn { Header="Title",Binding=new Binding("Title"),Width=220 });
            var sourceStyle=new Style(typeof(TextBlock));sourceStyle.Setters.Add(new Setter(TextBlock.ToolTipProperty,new Binding("ScanRoot")));
            grid.Columns.Add(new DataGridTextColumn { Header="File source",Binding=new Binding("ScanRoot"),ElementStyle=sourceStyle,IsReadOnly=true,Width=260 });
            grid.Columns.Add(new DataGridTextColumn { Header="Status",Binding=new Binding("Status"),IsReadOnly=true,Width=105 });
            grid.Columns.Add(new DataGridTextColumn { Header="Executable",Binding=new Binding("Executable"),IsReadOnly=true,Width=270 });
            grid.Columns.Add(new DataGridTextColumn { Header="Metadata",Binding=new Binding("MetadataStatus"),IsReadOnly=true,Width=260 });
            grid.Columns.Add(new DataGridTextColumn { Header="Reason",Binding=new Binding("Reason"),IsReadOnly=true,MinWidth=240,Width=new DataGridLength(1,DataGridLengthUnitType.Star) });
            panel.Children.Add(grid);
            var choices=new ComboBox { Margin=new Thickness(5),MinWidth=500,HorizontalAlignment=HorizontalAlignment.Stretch };
            bottom.Children.Add(Text("Launcher for the selected row (.exe / .bat / .cmd; discovery does not run it):"));bottom.Children.Add(choices);
            var arguments=new TextBox { Margin=new Thickness(5),MinWidth=500 };
            arguments.SetBinding(TextBox.TextProperty,new Binding("Arguments") { Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });
            bottom.Children.Add(Text("Optional launch arguments for this game (copy your known working arguments; leave blank otherwise):"));bottom.Children.Add(arguments);
            var details=new Expander { Header="Working directory and game-stop tracking",Margin=new Thickness(5) };
            var detailPanel=new StackPanel();details.Content=detailPanel;bottom.Children.Add(details);
            var working=new TextBox { Margin=new Thickness(4) };
            working.SetBinding(TextBox.TextProperty,new Binding("WorkingDirectory") { Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });
            detailPanel.Children.Add(Text("Working directory inside this game's folder; blank uses the launcher's folder. Relative paths start at the game folder."));detailPanel.Children.Add(working);
            var tracking=new ComboBox { Margin=new Thickness(4),ItemsSource=new [] { "Default","Process","Folder" } };
            tracking.SetBinding(ComboBox.SelectedItemProperty,new Binding("Tracking") { Mode=BindingMode.TwoWay,UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged });
            detailPanel.Children.Add(Text("Game-stop tracking: Default, launcher and child processes, or processes running from the game folder. Check the first real exit/backup."));detailPanel.Children.Add(tracking);
            grid.SelectionChanged+=(s,e)=> {
                var row=grid.SelectedItem as GameRow;choices.ItemsSource=row==null?null:row.Candidates;
                choices.SelectedItem=row==null?null:row.Choice;
                arguments.DataContext=row;
                working.DataContext=row;tracking.DataContext=row;
            };
            var actions=new WrapPanel();bottom.Children.Add(actions);
            actions.Children.Add(Button("Use selected launcher",()=> {
                var row=grid.SelectedItem as GameRow;var choice=choices.SelectedItem as Candidate;
                if(row==null || choice==null || row.IsGroup || row.Status=="ADDED" || row.Status=="EXISTING")return;
                row.Choice=choice;row.Include=true;row.Status="MANUAL";row.Reason=choice.ScriptWrapper?"Command wrapper explicitly selected; script contents have not been safety-audited":"Launcher selected in review";grid.Items.Refresh();summary.Text=Summary(latest);
            }));
            actions.Children.Add(Button("Import checked",()=> {
                grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
                var selected=latest.Rows.Where(r=>r.Include && r.Choice!=null && r.Status!="ADDED" && r.Status!="EXISTING").ToList();
                if(selected.Any(r=>r.Status!="READY" && r.Status!="MANUAL")) {
                    api.Dialogs.ShowMessage("For a review/changed row, select its launcher and click Use selected launcher first.","Local Games");return;
                }
                RememberUnchecked();
                foreach(var row in selected) {
                    var folder=FolderFor(row.ScanRoot);if(folder!=null)folder.Excluded.RemoveAll(p=>String.Equals(p,Scanner.FolderKey(folder.Root,row.Folder),StringComparison.OrdinalIgnoreCase));
                }
                prefs.Automatic=auto.IsChecked==true;
                // All currently visible clear matches are accounted for by import or exclusion.
                if(latest.Error==null)foreach(var folder in prefs.Folders)
                    if(!latest.RootErrors.ContainsKey(Scanner.Canonical(folder.Root)))folder.ReviewedRoot=Scanner.Canonical(folder.Root);
                Save();Import(selected,false);grid.Items.Refresh();summary.Text=Summary(latest);
            }));
            actions.Children.Add(Button("Save preferences",()=> {
                grid.CommitEdit(DataGridEditingUnit.Cell,true);grid.CommitEdit(DataGridEditingUnit.Row,true);
                RememberUnchecked();prefs.Automatic=auto.IsChecked==true;Save();grid.Items.Refresh();summary.Text=Summary(latest);
                api.Dialogs.ShowMessage(AutomaticEnabled()?"Automatic imports are enabled for reviewed folders. Newly added folders still wait for their first import review.":"Preferences saved. Automatic imports wait until you finish the first review with Import checked.","Local Games");
            }));
            actions.Children.Add(Button("Rescan",()=> { w.Close();Rescan(); }));
            actions.Children.Add(Button("Scan folders...",()=> { w.Close();ShowFolders(); }));
            actions.Children.Add(Button("Mark / unmark category folder",()=> {
                var row=grid.SelectedItem as GameRow;if(row==null || row.IsRoot)return;
                if(row.Status=="ADDED" || row.Status=="EXISTING") { api.Dialogs.ShowMessage("This folder is already represented in Playnite. Its existing entry will be preserved.","Local Games");return; }
                var folder=FolderFor(row.ScanRoot);if(folder==null)return;
                string key=Scanner.FolderKey(folder.Root,row.Folder);if(String.IsNullOrEmpty(key))return;
                if(row.IsGroup)folder.GroupedFolders.RemoveAll(p=>String.Equals(p,key,StringComparison.OrdinalIgnoreCase));
                else folder.GroupedFolders.Add(key);
                folder.ReviewedRoot="";Save();latest=null;w.Close();Rescan();
            }));
            actions.Children.Add(Button("Browse known launcher...",()=> {
                var row=grid.SelectedItem as GameRow;if(row==null || row.IsGroup || row.IsRoot || row.Status=="ADDED" || row.Status=="EXISTING")return;
                var folder=FolderFor(row.ScanRoot);if(folder==null)return;
                string path=api.Dialogs.SelectFile("Game launchers|*.exe;*.bat;*.cmd",row.Folder);if(String.IsNullOrEmpty(path))return;
                var choice=Scanner.ManualCandidate(folder.Root,row,path,folder.GroupedFolders.ToArray());
                if(choice==null) { api.Dialogs.ShowErrorMessage("Select a readable EXE/BAT/CMD inside this game folder. Linked paths and known installer helpers are not accepted.","Local Games");return; }
                row.Candidates.RemoveAll(c=>String.Equals(c.Path,choice.Path,StringComparison.OrdinalIgnoreCase));row.Candidates.Add(choice);
                row.Choice=choice;row.Status="MANUAL";row.Include=true;
                row.Reason="Known launcher selected manually; the original scan may remain incomplete. No launch was performed.";
                choices.ItemsSource=null;choices.ItemsSource=row.Candidates;choices.SelectedItem=choice;grid.Items.Refresh();summary.Text=Summary(latest);
            }));
            actions.Children.Add(Button("Select imports for metadata",()=> {
                var ids=ImportedGames();
                if(ids.Count==0) { api.Dialogs.ShowMessage("No games imported in this Playnite session are available to select.","Local Games");return; }
                api.MainView.SwitchToLibraryView();api.MainView.SelectGames(ids);w.Close();
                api.Dialogs.ShowMessage("Imported games from this session are selected. Open Main menu > Library > Download metadata. Choose Selected games only and enable Only download missing metadata to preserve filled fields. Include Cover image, Background image, Description and your other usual fields; Playnite uses your configured providers. The importer has not started a download. Review missing or uncertain matches in Playnite's game editor, then return here to Refresh metadata status.","Local Games");
            }));
            actions.Children.Add(Button("Refresh metadata status",()=> { RefreshMetadataStatus();grid.Items.Refresh(); }));
            actions.Children.Add(Button("Close",()=>w.Close()));
            w.Closed+=(s,e)=>window=null;
            w.Show();
        }
    }
}
