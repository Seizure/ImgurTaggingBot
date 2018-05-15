using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class, executed by the Discord.Commands API; see <see cref="DiscordCommandBase"/>.
 /// Implements commands for displaying the details of a particular Registered User in a particular Taglist,
 /// namely their preferences for that Taglist.
 /// Users can be specified by their Imgur username or Imgur account ID;
 /// this command does not access the Imgur API.
 /// </summary>
 public class QueryTaglistUserCommand : TaglistCommandsBase{
  public QueryTaglistUserCommand(TaglistRepository Repository,Discord.MessageSender MessageSender)
  :base(Repository,MessageSender){}

  [Command("TaglistUser")]
  [Summary("Displays details of a particular Registered User in a particular Taglist.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist which the user is Registered in.")]
   string TaglistName,
   [Summary("The Imgur username of the User Registered in the Taglist, for which to retrieve information on.")]
   string Username
  ){
   Log.Application_.LogVerbose("Executing TaglistUser for username '{0}'",Username);
   Taglist ModelTaglist = await ReadTaglist(TaglistName);
   if(ModelTaglist is null){return;}
   TaglistRegisteredUser Model;
   if( ! ModelTaglist.TryGetRegisteredUser(Username,out Model) ){
    await base.ReplyAsync(string.Format(
     "No Registered User in the Taglist '{1}' has the username '{0}'",
     Username, ModelTaglist.Name
    ));
    return;
   }
   string Response = RenderUserDetails(Model);
   await base.ReplyAsync( Response );
  }

  [Command("TaglistUser")]
  [Summary("Displays details of a particular Registered User in a particular Taglist.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist which the user is Registered in.")]
   string TaglistName,
   [Summary("The numeric Imgur account ID of the User Registered in the Taglist, for which to retrieve information on.")]
   int UserID
  ){
   Log.Application_.LogVerbose("Executing TaglistUser for user ID '{0:D}'",UserID);
   Taglist ModelTaglist = await ReadTaglist(TaglistName);
   if(ModelTaglist is null){return;}
   TaglistRegisteredUser Model;
   if( ! ModelTaglist.TryGetRegisteredUser(UserID,out Model)){
    await base.ReplyAsync(string.Format(
     "No Registered User in the Taglist '{1}' has the user ID '{0:D}'",
     UserID, ModelTaglist.Name
    ));
    return;
   }
   string Response = RenderUserDetails(Model);
   await base.ReplyAsync( Response );
  }

  protected string RenderUserDetails(TaglistRegisteredUser Model){
   return string.Format(
    "Imgur Username — {1}"
    +"\r\nImgur Account ID — {0}"
    +"\r\nRatings interested in — {2}"
    +"\r\nCategory Blacklist — {3}",
    Model.ID,
    Model.Username,
    Model.AcceptedRatings,
    string.Join(", ",Model.CategoryBlacklist)
   );
  }
 }
}