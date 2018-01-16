using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Imgur.API.Models;
using Tagaroo.Imgur;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 public class ProcessLatestCommentsActivity{
  protected readonly ImgurInterfacer Imgur;
  protected readonly SettingsRepository RepositorySettings;
  protected readonly ProcessCommentActivity SubActivity;
  public ProcessLatestCommentsActivity(ImgurInterfacer Imgur,SettingsRepository RepositorySettings,ProcessCommentActivity SubActivity){
   this.Imgur=Imgur;
   this.RepositorySettings=RepositorySettings;
   this.SubActivity=SubActivity;
  }

  public async Task Execute(Settings CurrentSettings){
   IDictionary<string,IList<IComment>> NewComments;
   try{
    NewComments = await Imgur.ReadCommentsSince(
     CurrentSettings.CommentsProcessedUpToInclusive,
     CurrentSettings.CommenterUsernames,
     CurrentSettings.RequestThreshholdPullCommentsPerUser
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
     Tasks.Add(SubActivity.ExecuteIfNew(NewUserComment));
     if( NewUserComment.DateTime > LatestCommentAt ){
      LatestCommentAt = NewUserComment.DateTime;
     }
    }
   }
   //Wait until all Comments have been processed before then updating the latest comment date–time, in case of any unhandled exceptions during processing
   await Task.WhenAll(Tasks);
   if(LatestCommentAt > CurrentSettings.CommentsProcessedUpToInclusive){
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
   await Imgur.LogRemainingBandwidth();
  }
 }
}