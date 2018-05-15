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
 /// Implements a command for displaying a list of all the Taglists the application manages,
 /// not including all the Registered Users of those Taglists.
 /// </summary>
 public class QueryTaglistsCommand : TaglistCommandsBase{
  public QueryTaglistsCommand(TaglistRepository Repository,Discord.MessageSender MessageSender)
  :base(Repository,MessageSender){}

  [Command("Taglists")]
  [Summary("Displays a list of all Taglists.")]
  public async Task Execute(
  ){
   Log.Application_.LogVerbose("Executing 'Taglists' command");
   IReadOnlyCollection<Taglist> Model = await ReadAllTaglists();
   if(Model is null){return;}
   string Response = RenderTaglists(Model);
   await base.ReplyAsync( Response );
  }

  protected string RenderTaglists(IReadOnlyCollection<Taglist> Taglists){
   StringBuilder Result=new StringBuilder();
   foreach(Taglist thisTaglist in Taglists){
    Result.Append(thisTaglist.Name+"\r\n");
   }
   return Result.ToString();
  }
 }
}