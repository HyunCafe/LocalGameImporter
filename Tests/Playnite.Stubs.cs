// Test-only in-memory Playnite and dispatcher. Never distributed as runtime dependencies.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
namespace System.Windows { public sealed class Window { public void Activate(){} } }
namespace Playnite.SDK.Models {
 public enum GameActionType { File,URL }
 public enum TrackingMode { Default,Process,Directory }
 public class GameAction { public string Name,Path,WorkingDir,Arguments,TrackingPath;public int InitialTrackingDelay;public bool IsPlayAction;public GameActionType Type;public TrackingMode TrackingMode; }
 public class Game { public Guid Id=Guid.NewGuid();public string Name,InstallDirectory,CoverImage,BackgroundImage,Description,Icon;public bool IsInstalled;public ObservableCollection<GameAction> GameActions;public List<Guid> PlatformIds; }
 public class Platform { public Guid Id=Guid.NewGuid();public string Name; }
}
namespace Playnite.SDK {
 using Playnite.SDK.Models;
 public enum NotificationType { Info,Error }
 public class NotificationMessage { public string Text;public NotificationMessage(string id,string text,NotificationType type,Action action){Text=text;} }
 public class FakeNotifications { public string Last;public void Add(NotificationMessage n){Last=n.Text;}public void Remove(string id){} }
 public class FakeGames:List<Game> { public bool ThrowAfterAdd;public new void Add(Game g){base.Add(g);if(ThrowAfterAdd)throw new Exception();}public Game Get(Guid id){return this.FirstOrDefault(g=>g.Id==id);} }
 public class FakeDatabase { public FakeGames Games=new FakeGames();public List<Platform> Platforms=new List<Platform>(); }
 public class FakeDispatcher { public ConcurrentQueue<Action> Queue=new ConcurrentQueue<Action>();public bool HasShutdownStarted=false;public bool CheckAccess(){return true;}public void BeginInvoke(Action a){Queue.Enqueue(a);}public void Drain(){Action a;while(Queue.TryDequeue(out a))a();} }
 public class FakeView { public FakeDispatcher UIDispatcher=new FakeDispatcher();public List<Guid> Selected=new List<Guid>();public void SelectGames(IEnumerable<Guid> ids){Selected=ids.ToList();} }
 public interface IPlayniteAPI { FakeDatabase Database{get;}FakeView MainView{get;}FakeNotifications Notifications{get;} }
 public class FakeAPI:IPlayniteAPI { public FakeDatabase Database{get;private set;}public FakeView MainView{get;private set;}public FakeNotifications Notifications{get;private set;}public FakeAPI(){Database=new FakeDatabase();MainView=new FakeView();Notifications=new FakeNotifications();} }
}
