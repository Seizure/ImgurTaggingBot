using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;

namespace Tagaroo.Discord.Commands{
 public class QueryTaglistUserCommand : TaglistCommandsBase{
  public QueryTaglistUserCommand(TaglistRepository Repository)
  :base(Repository){}

  [Command("TaglistUser")]
  [Summary("Displays details of a particular Registered User in a particular Taglist.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist which the user is Registered in.")]
   string TaglistName,
   [Summary("The Imgur username of the User Registered in the Taglist, for which to retrieve information on.")]
   string Username
  ){
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