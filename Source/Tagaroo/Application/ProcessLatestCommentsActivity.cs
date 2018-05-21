using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Imgur.API.Models;
using Tagaroo.Imgur;
using Tagaroo.DataAccess;
using Tagaroo.Model;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class.
 /// Retrieves any new Imgur Comments from all of the Commenters specified in <see cref="Settings.CommenterUsernames"/>,
 /// using the associated <see cref="ImgurInterfacer"/> Service.
 /// Then, for each new Comment, the associated <see cref="ProcessCommentActivity"/> activity is executed.
 /// After this, <see cref="Settings.CommentsProcessedUpToInclusive"/> is updated and saved via <see cref="SettingsRepository"/>
 /// with the date–time of the most recent Comment from all the Commenters.
 /// Specific behavior is controlled by the <see cref="Settings"/> object returned by the associated <see cref="SettingsRepository"/>,
 /// an instance of <see cref="Settings"/> read per-execution of the activity.
 /// As a performance enhancement, this activity initializes a <see cref="CacheingTaglistRepository"/> Decorator of <see cref="TaglistRepository"/>,
 /// which should also be passed to the downstream <see cref="ProcessTagCommandActivity"/>;
 /// the downstream <see cref="ProcessTagCommandActivity"/> will be executed potentially multiple times,
 /// each time executing a read from its associated <see cref="TaglistRepository"/>;
 /// the <see cref="CacheingTaglistRepository"/> Decorator caches reads from the Taglists data store,
 /// eliminating multiple reads of the data store from potential sequential executions of the downstream activity.
 /// </summary>
 public class ProcessLatestCommentsActivity{
  protected readonly ImgurInterfacer Imgur;
  protected readonly SettingsRepository RepositorySettings;
  protected readonly ProcessCommentActivity SubActivity;
  private readonly CacheingTaglistRepository RepositoryTaglists;
  public ProcessLatestCommentsActivity(
   ImgurInterfacer Imgur,
   SettingsRepository RepositorySettings,
   CacheingTaglistRepository RepositoryTaglists,
   ProcessCommentActivity SubActivity
  ){
   this.Imgur=Imgur;
   this.RepositorySettings=RepositorySettings;
   this.SubActivity=SubActivity;
   this.RepositoryTaglists=RepositoryTaglists;
  }

  //Some exceptions may go unobserved in this method, as not all created Tasks are awaited if problems occur
  /// <summary>
  /// <para>Preconditions: The associated <see cref="DiscordInterfacer"/> of the indirectly-associated <see cref="ProcessTagCommandActivity"/> is in a Connected state</para>
  /// </summary>
  public async Task Execute(){
   Log.Application_.LogVerbose("Starting processing of latest Comments");
   Settings CurrentSettings;
   Task<Settings> CurrentSettingsTask = RepositorySettings.LoadSettings();
   //Robustness — clear the cache in case it hasn't been cleared for some reason
   RepositoryTaglists.ClearCache();
   /*
   Initialize the Taglist Repository cache, so that subsequent calls to Load
   by the ProcessCommentActivity SubActivity tasks
   will efficiently retrieve the cached result
   */
   //TODO Implement proper lazy-loading in CacheingTaglistRepository, so that results are cached upon calls to Load and not just LoadAll
   Task InitializeCacheTask = RepositoryTaglists.LoadAll();
   try{
    CurrentSettings = await CurrentSettingsTask;
   }catch(DataAccessException Error){
    Log.Application_.LogError("Error loading Settings while pulling latest Comments from Imgur; pull aborted: "+Error.Message);
    return;
   }
   Task<IDictionary<string,IList<IComment>>> NewCommentsTask = Imgur.ReadCommentsSince(
    CurrentSettings.CommentsProcessedUpToInclusive,
    CurrentSettings.CommenterUsernames,
    CurrentSettings.RequestThreshholdPullCommentsPerUser
   );
   IDictionary<string,IList<IComment>> NewComments;
   try{
    NewComments = await NewCommentsTask;
   }catch(ImgurException Error){
    Log.Application_.LogError("Error pulling latest Comments from Imgur: "+Error.Message);
    return;
   }
   try{
    await InitializeCacheTask;
   }catch(DataAccessException Error){
    Log.Application_.LogError("Error loading Taglists while pulling Imgur Comments; Comment processing aborted: "+Error.Message);
    return;
   }
   //Execute the sub-activity for each new Comment, keeping track of the latest date–time from all the Comments
   Log.Application_.LogVerbose("Processing latest Comments from {0} total Commenters",NewComments.Count);
   bool AnyCommentsProcessed=false;
   DateTimeOffset LatestCommentAt=DateTimeOffset.MinValue;
   foreach( KeyValuePair<string,IList<IComment>> _NewUserComments in NewComments ){
    IList<IComment> NewUserComments=_NewUserComments.Value;
    //Process Comments from a particular user, oldest Comment first
    Log.Application_.LogVerbose("Processing latest Comments from Commenter '{0}', total — {1}",_NewUserComments.Key,NewUserComments.Count);
    foreach( IComment NewUserComment in NewUserComments.Reverse() ){
     bool CommentsProcessed = await SubActivity.ExecuteIfNew(NewUserComment);
     if(CommentsProcessed){
      AnyCommentsProcessed=true;
     }
     if( NewUserComment.DateTime > LatestCommentAt ){
      LatestCommentAt = NewUserComment.DateTime;
     }
    }
   }
   RepositoryTaglists.ClearCache();
   //Update and save the latest Comment date–time if it has progressed
   Log.Application_.LogVerbose("Latest date–time of all Comments read — {0:u}; current saved value is {1:u}",LatestCommentAt,CurrentSettings.CommentsProcessedUpToInclusive);
   if(LatestCommentAt > CurrentSettings.CommentsProcessedUpToInclusive){
    Log.Application_.LogVerbose("Updating and saving latest processed Comment date–time");
    CurrentSettings.CommentsProcessedUpToInclusive = LatestCommentAt;
    try{
     await RepositorySettings.SaveWritableSettings(CurrentSettings);
    }catch(DataAccessException Error){
     Log.Application_.LogError(
      "Unable to save updated Settings with new 'Last Processed Comment date–time' of {0:u}; it would be advisable, though not essential, to update the settings file manually with the updated value. Details: {1}",
      CurrentSettings.CommentsProcessedUpToInclusive,Error.Message
     );
    }
   }
   if(AnyCommentsProcessed){
    await Imgur.LogRemainingBandwidth(TraceEventType.Information);
   }else{
    await Imgur.LogRemainingBandwidth(TraceEventType.Verbose);
   }
  }
 }

 /// <summary>
 /// A <see cref="TaglistRepository"/> Decorator that adds cacheing functionality.
 /// Specifically, a call to <see cref="LoadAll"/> will store the result in the cache.
 /// The cached result or a relevant part thereof is then returned immediately by
 /// subsequent calls to <see cref="LoadAll"/>, <see cref="Load"/>, and <see cref="ReadAllHeaders"/>,
 /// but not <see cref="LoadAndLock"/>.
 /// The cache is cleared by <see cref="ClearCache"/>, and also upon a call to <see cref="Save"/>.
 /// </summary>
 public class CacheingTaglistRepository : TaglistRepository{
  private readonly TaglistRepository Decorate;
  private IReadOnlyDictionary<string,Taglist> Cache=null;
  public CacheingTaglistRepository(TaglistRepository Decorate){
   this.Decorate=Decorate;
  }

  public void ClearCache(){
   this.Cache=null;
  }
  
  public void Initialize(){
   Decorate.Initialize();
  }

  public Task<IReadOnlyDictionary<string,Taglist>> ReadAllHeaders(){
   if(!(Cache is null)){
    return Task.FromResult(Cache);
   }
   return Decorate.ReadAllHeaders();
  }

  public async Task<IReadOnlyDictionary<string,Taglist>> LoadAll(){
   if(Cache is null){
    this.Cache = await Decorate.LoadAll();
   }
   return Cache;
  }

  public Task<Taglist> Load(string TaglistName){
   TaglistName = TaglistName.Normalize(NormalizationForm.FormKD);
   if(Cache is null){
    return Decorate.Load(TaglistName);
   }
   if(Cache.TryGetValue(TaglistName,out Taglist Result)){
    return Task.FromResult(Result);
   }
   return Task.FromException<Taglist>(new EntityNotFoundException());
  }

  public Task<Tuple<Taglist,Lock>> LoadAndLock(string TaglistName){
   return Decorate.LoadAndLock(TaglistName);
  }

  public async Task Save(Taglist ToSave,Lock Lock){
   await Decorate.Save(ToSave,Lock);
   this.Cache=null;
   /*
   if(!(Cache is null)){
    this.Cache[ToSave.Name]=ToSave;
   }
   */
  }
 }
}