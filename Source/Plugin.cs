using System;
using System.Collections.Generic;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Events;

[assembly: AssemblyTitle("Local Game Importer")]
[assembly: AssemblyCompany("HyunCafe")]
[assembly: AssemblyProduct("Local Game Importer")]
[assembly: AssemblyVersion("0.3.2.0")]
[assembly: AssemblyFileVersion("0.3.2.0")]
[assembly: AssemblyDescription("Scoped local-game discovery for Playnite; no network or game-file writes")]

namespace LocalGameImporter {
    public sealed class LocalGameImporterPlugin : GenericPlugin {
        bool initialized;
        public override Guid Id { get { return new Guid("90063177-622a-5e60-accd-b05fb2b16c0a"); } }
        public LocalGameImporterPlugin(IPlayniteAPI api) : base(api) { }
        bool Ready() {
            if(initialized)return true;
            try { Host.Initialize(PlayniteApi,GetPluginUserDataPath());initialized=true;return true; }
            catch {
                PlayniteApi.Notifications.Add("LocalGameImporter.Init","Local Games could not initialize. This package targets Playnite 10.63; see its README.",NotificationType.Error);
                return false;
            }
        }
        public override void OnApplicationStarted(OnApplicationStartedEventArgs args) { if(Ready())Host.StartupOrRefresh(); }
        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args) { if(initialized)Host.StartupOrRefresh(); }
        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args) { if(initialized)Host.Stop(); }
        public override void Dispose() { if(initialized)Host.Stop();base.Dispose(); }
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args) {
            return new [] {
                new MainMenuItem { Description="Review games and automation",MenuSection="@Local Games",Action=a=> { if(Ready())Host.Review(); } },
                new MainMenuItem { Description="Rescan game folder",MenuSection="@Local Games",Action=a=> { if(Ready())Host.Rescan(); } },
                new MainMenuItem { Description="Cancel current scan",MenuSection="@Local Games",Action=a=> { if(initialized)Host.Cancel(); } }
            };
        }
    }
}
