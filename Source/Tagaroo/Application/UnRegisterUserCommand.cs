using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using DiscordCommandBase=Tagaroo.Discord.DiscordCommandBase;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class, executed by the Discord.Commands API; see <see cref="DiscordCommandBase"/>.
 /// Implements commands for UnRegistering a Registered User from a Taglist, using the <see cref="Taglist.UnRegisterUser"/> method.
 /// The Registered User to UnRegister can be specified by either the username or ID of their Imgur user account.
 /// The <see cref="Taglist"/> to UnRegister them from will be briefly locked during execution of this activity via <see cref="TaglistRepository.LoadAndLock"/>,
 /// and then persisted using <see cref="TaglistRepository.Save"/>.
 /// </summary>
 public class UnRegisterUserCommand : DiscordCommandBase{
  private readonly TaglistRepository Repository;
  public UnRegisterUserCommand(TaglistRepository Repository,Discord.MessageSender MessageSender):base(MessageSender){
   this.Repository=Repository;
  }

  [Command("UnRegister")]
  [Summary("Unregisters a User from a Taglist. The user will be removed from the Taglist's Registered Users.")]
  public async Task Execute(
   [Summary("The Imgur username of the Registered User to Unregister.")]
   string Username,
   [Summary("The name of the Taglist to Unregister the User from.")]
   string TaglistName
  ){
   Log.Application_.LogVerbose("Executing 'UnRegister' command for username '{0}' from Taglist '{1}'",Username,TaglistName);
   string Response = await ExecuteUnregistration(Username,null,TaglistName);
   await base.ReplyAsync(Response);
  }

  [Command("UnRegister")]
  public async Task Execute(
   [Summary("The Imgur account ID of the Registered User to Unregister.")]
   int UserID,
   string TaglistName
  ){
   Log.Application_.LogVerbose("Executing 'UnRegister' command for user ID '{0}' from Taglist '{1}'",UserID,TaglistName);
   string Response = await ExecuteUnregistration(null,UserID,TaglistName);
   await base.ReplyAsync(Response);
  }

  private async Task<string> ExecuteUnregistration(string Username,int? UserID,string TaglistName){
   Taglist Model;
   Lock Lock;
   try{
    Tuple<Taglist,Lock> LoadResult = await Repository.LoadAndLock(TaglistName);
    Model=LoadResult.Item1;
    Lock=LoadResult.Item2;
   }catch(EntityNotFoundException){
    return string.Format("No Taglist with the name '{0}' exists",TaglistName);
   }catch(DataAccessException Error){
    return "Error retrieving Taglist data: "+Error.Message;
   }
   try{
    TaglistRegisteredUser Removed;
    if(UserID is null){
     if( (Removed = Model.UnRegisterUser(Username)) is null ){
      return string.Format("No Registered User with the username '{0}' exists in the Taglist '{1}'",Username,Model.Name);
     }
    }else{
     if( (Removed = Model.UnRegisterUser(UserID.Value)) is null ){
      return string.Format("No Registered User with the ID '{0:D}' exists in the Taglist '{1}'",UserID,Model.Name);
     }
    }
    try{
     await Repository.Save(Model,Lock);
    }catch(DataAccessException Error){
     return "Error saving updated Taglist: "+Error.Message;
    }
    return string.Format(
     "User '{1}' (ID {0:D}) Unregistered from Taglist '{2}'",
     Removed.ID,
     Removed.Username,
     Model.Name
    );
   }finally{
    Lock.Release();
   }
  }
 }
}