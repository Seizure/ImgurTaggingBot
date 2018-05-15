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
 /// Implements commands for displaying a list of Registered Users in a particular Taglist.
 /// </summary>
 public class QueryTaglistUsersCommand : TaglistCommandsBase{
  public QueryTaglistUsersCommand(TaglistRepository Repository,Discord.MessageSender MessageSender)
  :base(Repository,MessageSender){}

  [Command("Taglist")]
  [Summary("Displays all Users Registered to a Taglist.")]
  public async Task Execute(
   [Summary("The identifying name of the Taglist for which to retrieve information on.")]
   string TaglistName
  ){
   Log.Application_.LogVerbose("Executing 'Taglist' command for Taglist '{0}'",TaglistName);
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
   Log.Application_.LogVerbose("Executing 'Taglist' command for Taglist '{0}', Rating '{1}', and {2} total Categories",TaglistName,RatingSpecifier,Categories.Length);
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