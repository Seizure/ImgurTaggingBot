using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;

namespace Tagaroo.Discord.Commands{
 public class QueryUserTaglistsCommand : TaglistCommandsBase{
  public QueryUserTaglistsCommand(TaglistRepository Repository)
  :base(Repository){}

  [Command("UserTaglists")]
  [Summary("Searches through all Taglists and displays those that contain a Registered User with the specified username.")]
  public async Task Execute(
   [Summary("The Imgur username of the Registered User to search for.")]
   string Username
  ){
   IReadOnlyCollection<Taglist> AllTaglists = await ReadAllTaglistsAndUsers();
   if(AllTaglists is null){return;}
   ICollection<Taglist> Result = (
    from TL in AllTaglists
    where TL.hasRegisteredUser(Username)
    select TL
   ).ToList();
   await DisplayResults(
    Result,
    string.Format("Taglists that username '{0}' appears in:",Username)
   );
  }

  [Command("UserTaglists")]
  [Summary("Searches through all Taglists and displays those that contain a Registered User with the specified user ID.")]
  public async Task Execute(
   [Summary("The Imgur account ID of the Registered User to search for.")]
   int UserID
  ){
   IReadOnlyCollection<Taglist> AllTaglists = await ReadAllTaglistsAndUsers();
   if(AllTaglists is null){return;}
   ICollection<Taglist> Result = (
    from TL in AllTaglists
    where TL.hasRegisteredUser(UserID)
    select TL
   ).ToList();
   await DisplayResults(
    Result,
    string.Format("Taglists that user ID '{0:D}' appears in:",UserID)
   );
  }

  protected async Task DisplayResults(ICollection<Taglist> Results,string Heading){
   if(Results.Count<=0){
    await base.ReplyAsync(
     Heading
     +string.Format("\r\nNo Taglists Found")
    );
    return;
   }
   StringBuilder ResultsList=new StringBuilder(Heading);
   foreach(Taglist Result in Results){
    ResultsList.Append("\r\n"+Result.Name);
   }
   await base.ReplyAsync(
    ResultsList.ToString()
   );
  }
 }
}