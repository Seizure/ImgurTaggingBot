using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Imgur.API.Models;
using Tagaroo.Model;
using Tagaroo.Imgur;
using Tagaroo.Discord;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 public class CoreProcess : ImgurCommandHandler{
  private readonly ImgurInterfacer Imgur;
  private readonly DiscordInterfacer Discord;
  private readonly ImgurCommandParser CommandParserImgur=new ImgurCommandParser();
  private readonly TaglistRepository RepositoryTaglists;
  private readonly Settings Settings;
  private readonly SingleThreadSynchronizationContext ApplicationMessagePump = new SingleThreadSynchronizationContext();
  private readonly CancellationTokenSource ShutdownSignal=new CancellationTokenSource();
  
  public CoreProcess(ImgurInterfacer Imgur, DiscordInterfacer Discord, TaglistRepository RepositoryTaglists, Settings Settings){
   this.Imgur=Imgur;
   this.Discord=Discord;
   this.RepositoryTaglists=RepositoryTaglists;
   this.Settings=Settings;
  }

  public void Run(){
   ApplicationMessagePump.RunOnCurrentThread(async()=>{
    bool success = await Setup();
    if(!success){return;}
    await RunMain();
    await UnSetup();
    ApplicationMessagePump.Finish();
   });
  }

  private async Task<bool> Setup(){
   Log.Bootstrap_.LogInfo("Connecting to Discord...");
   try{
    await Discord.Connect();
   }catch(DiscordException Error){
    Log.Bootstrap_.LogError("Unable to connect to Discord: "+Error.Message);
    return false;
   }
   return true;
  }

  private async Task UnSetup(){
   await Discord.Shutdown();
  }

  private async Task RunMain(){
   Log.Bootstrap_.LogInfo("Setup complete, running application");
   bool run=true;
   while(run){
    await ProcessLatestComments();
    try{
     await Task.Delay(6000);
     this.Shutdown();
     await Task.Delay(
      Settings.PullCommentsFrequency,
      ShutdownSignal.Token
     );
    }catch(TaskCanceledException){
     run=false;
     Log.Bootstrap_.LogInfo("Received shutdown signal");
    }
   }
  }

  public void Shutdown(){
   ShutdownSignal.Cancel();
  }

  public async Task ProcessLatestComments(){
   IDictionary<string,IList<IComment>> NewComments;
   try{
    NewComments = await Imgur.ReadCommentsSince(
     Settings.CommentsProcessedUpToInclusive,
     Settings.CommenterUsernames,
     Settings.RequestThreshholdPullCommentsPerUser
    );
   }catch(ImgurException Error){
    Log.Application_.LogError("Error pulling latest Comments from Imgur: "+Error.Message);
    return;
   }
   List<Task> Tasks=new List<Task>();
   DateTimeOffset LatestCommentAt=DateTimeOffset.MinValue;
   foreach( IList<IComment> NewUserComments in NewComments.Values ){
    //Process Comments from a particular user, oldest Comment first
    foreach( IComment NewUserComment in NewUserComments.Reverse() ){
     Tasks.Add(ProcessComment(NewUserComment));
     if( NewUserComment.DateTime > LatestCommentAt ){
      LatestCommentAt = NewUserComment.DateTime;
     }
    }
   }
   await Task.WhenAll(Tasks);
  }

  public async Task ProcessComment(IComment Process){
   //Skip Comments that have already been replied to by this application; otherwise Comments will be re-processed
   if(Process.Children.Any(
    C => Imgur.isCommentByThisApplication(C)
   )){
    return;
   }
   await ProcessCommentUnconditionally(Process);
  }
  
  public async Task ProcessCommentUnconditionally(IComment Process){
   await CommandParserImgur.ProcessCommands(Process,this);
  }

  /*
  void ImgurCommandHandler.ProcessTagCommand(Tag CommandParameter){
   #pragma warning disable CS4014 //Any unhandled exceptions after the first await will be silently swallowed, unless ThrowUnobservedTaskExceptions is specified
   this.ProcessTagCommand(CommandParameter);
   #pragma warning restore CS4014
  }
  */
  public async Task ProcessTagCommand(Tag Command){
   GalleryItem TaggedItem;
   try{
    if(!Command.isItemAlbum){
     TaggedItem=await Imgur.ReadGalleryImage(Command.ItemID);
    }else{
     TaggedItem=await Imgur.ReadGalleryAlbum(Command.ItemID);
    }
   }catch(ImgurException Error){
    Log.Application_.LogError("Error acquiring details for Tagged Imgur Gallery item with ID '{0}': {1}",Command.ItemID,Error.Message);
    return;
   }
   IReadOnlyDictionary<string,Taglist> AllTaglists;
   try{
    AllTaglists = await RepositoryTaglists.LoadAll();
   }catch(DataAccessException Error){
    Log.Application_.LogError("Error loading Taglists while processing Tag command: "+Error.Message);
    return;
   }
   if( !AllTaglists.TryGetValue(Command.TaglistName,out Taglist SpecifiedTaglist) ){
    Log.Application_.LogWarning(
     "The Taglist named '{0}' does not exist, which was specified by a Tag command on the Imgur Gallery item with ID '{1}' (Comment ID {2:D})",
     Command.TaglistName,Command.ItemID,Command.HostCommentID
    );
    return;
   }
   await Task.WhenAll(
    ProcessTagCommand_MentionInterestedUsers(Command,SpecifiedTaglist),
    ProcessTagCommand_ArchiveTaggedItem(Command,TaggedItem,SpecifiedTaglist)
   );
  }
  
  private async Task ProcessTagCommand_MentionInterestedUsers(Tag Command,Taglist SpecifiedTaglist){
   ISet<string> InterestedUsernames = SpecifiedTaglist.FilterByUsersInterestedIn( Command.Rating, Command.Categories );
   try{
    await Imgur.MentionUsers(Command.ItemID, Command.HostCommentID, InterestedUsernames);
   }catch(ImgurException Error){
    Log.Application_.LogError(
     "Error Mentioning users in Taglist '{1}' in response to the Tag command on the Imgur Gallery item with ID '{0}': {2}",
     Command.ItemID,Command.TaglistName,Error.Message
    );
    return;
   }
  }

  private async Task ProcessTagCommand_ArchiveTaggedItem(Tag Command,GalleryItem ToArchive,Taglist SpecifiedTaglist){
   try{
    await Discord.PostGalleryItemDetails(
     SpecifiedTaglist.ArchiveChannelIDForRating(Command.Rating),
     ToArchive
    );
   }catch(DiscordException Error){
    Log.Application_.LogError(
     "Error archiving Imgur Gallery item with ID '{0}' to the relevant Archive channel for Taglist '{1}': {2}",
     ToArchive.ID,SpecifiedTaglist.Name,Error.Message
    );
   }
  }
 }
}