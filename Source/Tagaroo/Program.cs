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
 public class Program{
  private readonly ImgurInterfacer Imgur;
  private readonly DiscordInterfacer Discord;
  private readonly TaglistRepository RepositoryTaglists;
  private readonly SettingsRepository RepositorySettings;
  private readonly ProcessLatestCommentsActivity ActivityProcessComments;
  private readonly SingleThreadSynchronizationContext ApplicationMessagePump = new SingleThreadSynchronizationContext();
  private readonly TaskScheduler Scheduler=new TaskScheduler();
  private readonly IServiceCollection DiscordCommandServices;
  private Settings CurrentSettings;
  private readonly TimeSpan PullCommentsFrequency;
  
  public Program(
   ProcessLatestCommentsActivity ActivityProcessComments,
   ImgurInterfacer Imgur,
   DiscordInterfacer Discord,
   TaglistRepository RepositoryTaglists,
   SettingsRepository RepositorySettings,
   TimeSpan PullCommentsFrequency
  ){
   if(PullCommentsFrequency < TimeSpan.Zero){
    throw new ArgumentOutOfRangeException(nameof(PullCommentsFrequency),"Cannot be negative or infinite");
   }
   this.Imgur=Imgur;
   this.Discord=Discord;
   this.RepositoryTaglists=RepositoryTaglists;
   this.RepositorySettings=RepositorySettings;
   this.ActivityProcessComments=ActivityProcessComments;
   this.PullCommentsFrequency=PullCommentsFrequency;
   this.DiscordCommandServices=new ServiceCollection()
    .AddSingleton<DiscordInterfacer>(Discord)
    .AddSingleton<ImgurInterfacer>(Imgur)
   ;
  }

  public bool Run(){
   bool startupsuccess=false;
   Log.Bootstrap_.LogVerbose("Beginning application message pump");
   ApplicationMessagePump.RunOnCurrentThread(async()=>{
    Log.Bootstrap_.LogVerbose("Application message pump started");
    bool success = await Setup();
    if(!success){
     ApplicationMessagePump.Finish();
     return;
    }
    startupsuccess=true;
    await RunMain();
    await UnSetup();
    ApplicationMessagePump.Finish();
   });
   return startupsuccess;
  }

  private async Task<bool> Setup(){
   Log.Bootstrap_.LogVerbose("Initializing repositories");
   RepositorySettings.Initialize();
   RepositoryTaglists.Initialize();
   Log.Bootstrap_.LogInfo("Reading Settings");
   try{
    this.CurrentSettings = await RepositorySettings.LoadSettings();
   }catch(DataAccessException Error){
    Log.Bootstrap_.LogError("Could not load Settings: "+Error.Message);
   }
   if(this.CurrentSettings is null){return false;}
   Discord.Initialize(DiscordCommandServices.BuildServiceProvider());
   Log.Bootstrap_.LogInfo("Connecting to Discord...");
   try{
    await Discord.Connect();
    Log.Bootstrap_.LogInfo("Discord connection established");
   }catch(DiscordException Error){
    Log.Bootstrap_.LogError("Unable to connect to Discord: "+Error.Message);
    return false;
   }
   bool result=false;
   try{
    Log.Bootstrap_.LogVerbose("Verifying Taglists");
    if(!await CheckTaglists()){
     return false;
    }
    Log.Bootstrap_.LogInfo("Making contact with Imgur");
    IRateLimit RemainingBandwidth;
    try{
     RemainingBandwidth = await Imgur.ReadRemainingBandwidth();
    }catch(ImgurException Error){
     Log.Bootstrap_.LogError("Error making initial contact with Imgur: "+Error.Message);
     return false;
    }
    Log.Bootstrap_.LogInfo(
     "Remaining Imgur API Bandwidth - {0:D} / {1:D}",
     RemainingBandwidth.ClientRemaining,RemainingBandwidth.ClientLimit
    );
    result = true;
   }finally{
    if(!result){
     await UnSetup();
    }
   }
   return result;
  }

  private async Task UnSetup(){
   await Discord.Shutdown();
  }

  private async Task<bool> CheckTaglists(){
   ICollection<Taglist> Taglists;
   try{
    Taglists = await RepositoryTaglists.ReadAllHeaders();
   }catch(DataAccessException Error){
    Log.Bootstrap_.LogError("Error accessing Taglists data: "+Error.Message);
    return false;
   }
   foreach(Taglist CheckChannels in Taglists){
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
  
  private bool CheckTaglistChannel(Taglist Check, ulong ChannelID, string ChannelName, bool ShouldBeNSFW=false){
   if(!Discord.TextChannelExists(ChannelID, out bool NSFW)){
    Log.Bootstrap_.LogError(
     "The '{1}' Channel for the Taglist '{0}' (the Channel specified by the ID {2}), does not exist in the Guild",
     Check.Name,ChannelName,ChannelID
    );
    return false;
   }
   if(ShouldBeNSFW && !NSFW){
    Log.Bootstrap_.LogWarning(
     "The '{1}' Channel for the Taglist '{0}' is not marked as NSFW",
     Check.Name,ChannelName
    );
   }
   return true;
  }

  private async Task RunMain(){
   Scheduler.AddTask(ScheduledTask.NewImmediateTask(
    PullCommentsFrequency,
    () => ActivityProcessComments.Execute(CurrentSettings)
   ));
   Log.Bootstrap_.LogInfo("Setup complete, running application");
   await Scheduler.Run();
   /*
   bool run=true;
   while(run){
    await ProcessLatestComments();
    try{
     //await Task.Delay(6000);
     //this.Shutdown();
     await Task.Delay(
      CurrentSettings.PullCommentsFrequency,
      ShutdownSignal.Token
     );
    }catch(TaskCanceledException){
     run=false;
     Log.Bootstrap_.LogInfo("Received shutdown signal");
    }
   }
   */
  }

  public void Shutdown(){
   Scheduler.Stop();
  }
 }
}