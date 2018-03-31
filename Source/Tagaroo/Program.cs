using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Imgur.API.Models;
using Tagaroo.Infrastructure;
using Tagaroo.Model;
using Tagaroo.Application;
using Tagaroo.Imgur;
using Tagaroo.Discord;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;
using TaskScheduler=Tagaroo.Infrastructure.TaskScheduler;

namespace Tagaroo{
 /// <summary>
 /// Performs application setup, then runs the application.
 /// Responsible for applying "requires restart" Settings,
 /// checking various things are correct before running,
 /// and placing objects into a state that a running application would expect them to be in,
 /// such as establishing a connection to Discord.
 /// </summary>
 public class Program{
  private readonly ImgurInterfacer Imgur;
  private readonly DiscordInterfacer Discord;
  private readonly TaglistRepository RepositoryTaglists;
  private readonly SettingsRepository RepositorySettings;
  private readonly ProcessLatestCommentsActivity ActivityProcessComments;
  private readonly SingleThreadSynchronizationContext ApplicationMessagePump
   = new SingleThreadSynchronizationContext( new NullSynchronizationContext() );
  private readonly TaskScheduler Scheduler=new TaskScheduler();
  private readonly IServiceCollection DiscordCommandServices;
  private readonly SingleThreadReadWriteLock ApplicationShutdownLock;
  private bool ReceivedShutdownSignal=false;
  
  /// <param name="ApplicationShutdownLock">
  /// This should be the same instance as passed to other objects;
  /// this class will acquire a write lock on the lock
  /// before allowing the process to shutdown cleanly
  /// </param>
  public Program(
   ProcessLatestCommentsActivity ActivityProcessComments,
   ImgurInterfacer Imgur,
   DiscordInterfacer Discord,
   TaglistRepository RepositoryTaglists,
   SettingsRepository RepositorySettings,
   SingleThreadReadWriteLock ApplicationShutdownLock
  ){
   this.Imgur=Imgur;
   this.Discord=Discord;
   this.RepositoryTaglists=RepositoryTaglists;
   this.RepositorySettings=RepositorySettings;
   this.ActivityProcessComments=ActivityProcessComments;
   this.DiscordCommandServices=new ServiceCollection()
    .AddSingleton<DiscordInterfacer>(Discord)
    .AddSingleton<ImgurInterfacer>(Imgur)
    .AddSingleton<TaglistRepository>(RepositoryTaglists)
    .AddSingleton<SettingsRepository>(RepositorySettings)
   ;
   this.ApplicationShutdownLock=ApplicationShutdownLock;
  }

  /// <summary>
  /// Runs the application, blocking until the application has finished,
  /// either due to being shut down normally, or a startup error.
  /// </summary>
  /// <returns>A success value — false if there was a problem setting up the application, preventing it from running, otherwise true</returns>
  public bool Run(){
   bool startupsuccess=false;
   Log.Bootstrap_.LogVerbose("Beginning application message pump");
   SynchronizationContext.SetSynchronizationContext(ApplicationMessagePump);
   ApplicationMessagePump.RunOnCurrentThread(async()=>{
    Log.Bootstrap_.LogVerbose("Application message pump started");
    Tuple<TimeSpan> SetupResults = await Setup();
    if(SetupResults is null){
     ApplicationMessagePump.Finish();
     return;
    }
    startupsuccess=true;
    //Execution will be within this method while the application is running
    await RunMain(SetupResults.Item1);
    Log.Bootstrap_.LogVerbose("Application has finished running; un-setting up");
    await UnSetup(true);
    ApplicationMessagePump.Finish();
   });
   Log.Bootstrap_.LogVerbose("Application message pump ended");
   return startupsuccess;
  }

  private async Task<Tuple<TimeSpan>> Setup(){
   Log.Bootstrap_.LogVerbose("Initializing repositories");
   RepositorySettings.Initialize();
   RepositoryTaglists.Initialize();
   Log.Bootstrap_.LogInfo("Reading Settings");
   Settings CurrentSettings=null;
   try{
    CurrentSettings = await RepositorySettings.LoadSettings();
   }catch(DataAccessException Error){
    Log.Bootstrap_.LogError("Could not load Settings: "+Error.Message);
   }
   if(CurrentSettings is null){return null;}
   Discord.Initialize( DiscordCommandServices.BuildServiceProvider(), ApplicationMessagePump );
   Log.Bootstrap_.LogInfo("Connecting to Discord...");
   try{
    await Discord.Connect();
    Log.Bootstrap_.LogInfo("Discord connection established");
   }catch(DiscordException Error){
    Log.Bootstrap_.LogError("Unable to connect to Discord: "+Error.Message);
    return null;
   }
   Tuple<TimeSpan> Result=null;
   try{
    Log.Bootstrap_.LogVerbose("Verifying Taglists");
    if(!await CheckTaglists()){
     return null;
    }
    Log.Bootstrap_.LogInfo("Making contact with Imgur");
    //TODO Make a call requiring the OAuth Token to ensure its validity
    IRateLimit RemainingBandwidth;
    try{
     RemainingBandwidth = await Imgur.ReadRemainingBandwidth();
    }catch(ImgurException Error){
     Log.Bootstrap_.LogError("Error making initial contact with Imgur: "+Error.Message);
     return null;
    }
    Log.Bootstrap_.LogInfo(
     "Remaining Imgur API Bandwidth - {0:D} / {1:D}",
     RemainingBandwidth.ClientRemaining,RemainingBandwidth.ClientLimit
    );
    Result = new Tuple<TimeSpan>(
     //Will always be positive and no greater than 2^31-1 milliseconds
     CurrentSettings.PullCommentsFrequency
    );
   }finally{
    if(Result is null){
     await UnSetup(false);
    }
   }
   return Result;
  }

  private async Task UnSetup(bool ApplicationWasRunning){
   //Acquire shutdown lock and never release
   Log.Bootstrap_.LogVerbose("Acquiring exclusive lock on shutdown lock...");
   await this.ApplicationShutdownLock.EnterWriteLock();
   Log.Bootstrap_.LogInfo("Shutdown lock acquired, shutting down");
   if(ApplicationWasRunning){
    Console.Out.WriteLine("No critical tasks now running; if the process hangs from this point forward, it is safe to forcibly terminate it");
   }
   await Discord.Shutdown();
  }

  //Ensures that all Taglists that the application manages are associated with Text Channels that exist in the Guild
  private async Task<bool> CheckTaglists(){
   IReadOnlyDictionary<string,Taglist> Taglists;
   try{
    Taglists = await RepositoryTaglists.ReadAllHeaders();
   }catch(DataAccessException Error){
    Log.Bootstrap_.LogError("Error accessing Taglists data: "+Error.Message);
    return false;
   }
   foreach(Taglist CheckChannels in Taglists.Values){
    Log.Bootstrap_.LogVerbose("Verifying Taglist '{0}'",CheckChannels.Name);
    if(!CheckTaglistChannel(CheckChannels, CheckChannels.ArchiveChannelIDSafe, "Safe Archive")){
     return false;
    }
    if(!CheckTaglistChannel(CheckChannels, CheckChannels.ArchiveChannelIDQuestionable, "Questionable Archive")){
     return false;
    }
    if(!CheckTaglistChannel(CheckChannels, CheckChannels.ArchiveChannelIDExplicit, "Explicit Archive", true)){
     return false;
    }
   }
   return true;
  }
  
  private bool CheckTaglistChannel(Taglist Check, ulong ChannelID, string ChannelTypeName, bool ShouldBeNSFW=false){
   if(!Discord.TextChannelExists(ChannelID, out string ChannelName, out bool NSFW)){
    Log.Bootstrap_.LogError(
     "The '{1}' Channel for the Taglist '{0}' (the Channel specified by the ID {2:D}), does not exist in the Guild",
     Check.Name,ChannelTypeName,ChannelID
    );
    return false;
   }
   if(ShouldBeNSFW && !NSFW){
    Log.Bootstrap_.LogWarning(
     "The '{1}' Channel for the Taglist '{0}' (Channel '{3}', #{2:D}) is not marked as NSFW",
     Check.Name,ChannelTypeName,ChannelID,ChannelName
    );
   }
   return true;
  }

  private Task RunMain(TimeSpan PullCommentsFrequency){
   Scheduler.AddTask(ScheduledTask.NewImmediateTask(
    //Comes from Settings.PullCommentsFrequency, so will always be positive and no greater than 2^31-1 milliseconds
    PullCommentsFrequency,
    () => ActivityProcessComments.Execute()
    //async() => {await Tests();Scheduler.Stop();}
   ));
   /*
   onShutdownSignal will synchronize a call to TaskScheduler.Stop using the current synchronization context,
   which will only have an effect if the TaskScheduler is running;
   the call is simply ignored if it is not running.
   In order to ensure that the TaskScheduler is running when the call is synchronized,
   only react to the CancelKeyPress event right before the TaskScheduler starts running;
   if a CancelKeyPress event gets fired at this point,
   then by the time it is synchronized to the current synchronization context the TaskScheduler will always be running.
   Any CancelKeyPress events that are fired before this point will have the default behavior
   of (presumably) immediately terminating all execution, which is acceptable before this point.
   */
   Console.CancelKeyPress+=onShutdownSignal;
   Log.Bootstrap_.LogInfo("Setup complete, running application");
   //Execution will be within this method while the application is running
   return Scheduler.Run();
  }

  private void onShutdownSignal(Object Origin,ConsoleCancelEventArgs Event){
   try{
    Event.Cancel = true;
   }catch(InvalidOperationException){
    return;
   }
   lock(ApplicationShutdownLock){
    if(ReceivedShutdownSignal){return;}
    this.ReceivedShutdownSignal=true;
   }
   Console.Out.WriteLine("Shutdown signal received; initiating shutdown...");
   Log.Bootstrap_.LogVerbose("Shutdown signal received");
   ApplicationMessagePump.Post(
    _=>Shutdown(),
    null
   );
  }

  /// <summary>
  /// Causes <see cref="Run"/> to return, once any currently running operation is complete.
  /// No effect if execution is not within <see cref="Run"/>.
  /// </summary>
  public void Shutdown(){
   Log.Bootstrap_.LogVerbose("Sending stop signal to task scheduler");
   Scheduler.Stop();
  }

  /*
  private async Task Tests(){
   try{
    Log.Application_.LogInfo("Loading all Taglists...");
    var ResultAllTaglists = await RepositoryTaglists.LoadAll();
    Log.Application_.LogInfo("Total Taglists - {0}",ResultAllTaglists.Count);
    Log.Application_.LogInfo("Loading SecondTaglist...");
    var ResultTaglist = await RepositoryTaglists.LoadAndLock("SecondTaglist");
    ResultTaglist.Item1.RegisterUser(new TaglistRegisteredUser("Added",int.MaxValue,TaglistRegisteredUser.RatingFlags.Safe,new string[]{"Category A","Category B"}));
    Log.Application_.LogInfo("Saving modified SecondTaglist...");
    await RepositoryTaglists.Save(ResultTaglist.Item1,ResultTaglist.Item2);
    Log.Application_.LogInfo("Reading Settings...");
    var ResultSettings = await RepositorySettings.LoadSettings();
    ResultSettings.CommentsProcessedUpToInclusive = DateTimeOffset.UtcNow;
    Log.Application_.LogInfo("Saving modified settings...");
    await RepositorySettings.SaveWritableSettings(ResultSettings);
    const string ToParse="@Tagaroo tag taglist S CategoryA CategoryB";
    Log.Application_.LogInfo("Parsing '{0}'...",ToParse);
    ImgurCommandParser Parser = new ImgurCommandParser("@Tagaroo",null);
    Parser.ParseTagCommand(ToParse,0,1,"Q",false,out Tag ResultParse);
    Log.Application_.LogInfo(
     "Host Comment ID - {0:D}; Tagged Item ID - {1}; Album - {2}; Taglist - '{3}'; Ratings - {4}; Categories - {5}",
     ResultParse.HostCommentID,ResultParse.ItemID,ResultParse.isItemAlbum,ResultParse.TaglistName,ResultParse.Rating,
     string.Join(",",ResultParse.Categories)
    );
    Log.Application_.LogError("Test Success");
   }catch(Exception Error){
    Log.Application_.LogInfo("Test Failure: "+Error.Message);
   }
  }
  */
 }
}