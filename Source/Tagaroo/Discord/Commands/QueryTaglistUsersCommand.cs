using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;

namespace Tagaroo.Discord.Commands{
 public class QueryTaglistUsersCommand : TaglistCommandsBase{
  public QueryTaglistUsersCommand(TaglistRepository Repository)
  :base(Repository){}

  [Command("Taglist")]
  [Summary("Displays all Users Registered to a Taglist.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist for which to retrieve information on.")]
   string TaglistName
  ){
   Taglist Model = await ReadTaglist(TaglistName);
   if(Model is null){return;}
   string Response = RenderUsers(Model.RegisteredUsers);
   await base.ReplyAsync( Response );
  }

  [Command("Taglist")]
  [Summary("Displays Users in a Taglist that are interested in Tagged items with a particular Rating and set of Categories. This can be used to simulate @Tagaroo Tag commands.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist for which to retrieve information on.")]
   string TaglistName,
   [Summary("The Rating to filter users by, specified as a single case-insensitive character, S for Safe, Q for Questionable, and E for Explicit.")]
   char RatingSpecifier,
   [Summary("The Categories to filter users by, whitespace separated, if any.")]
   params string[] Categories
  ){
   Ratings? FilterByRating = await ParseRating(RatingSpecifier);
   if(FilterByRating is null){return;}
   Taglist Model = await ReadTaglist(TaglistName);
   if(Model is null){return;}
   string Response = RenderUsers(
    Model.FilterByUsersInterestedIn( FilterByRating.Value, Categories.ToHashSet() )
   );
   await base.ReplyAsync( Response );
  }

  protected string RenderUsers(ICollection<TaglistRegisteredUser> Users){
   StringBuilder Result=new StringBuilder();
   Result.AppendFormat("Total Users — {0}\r\n",Users.Count);
   foreach(TaglistRegisteredUser thisUser in Users){
    Result.AppendFormat(
     "{1} [{0}]\r\n",
     thisUser.ID, thisUser.Username
    );
   }
   return Result.ToString();
  }
 }
}