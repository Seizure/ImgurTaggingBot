using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Discord.Commands;
using Tagaroo.Model;
using Tagaroo.DataAccess;

namespace Tagaroo.Discord.Commands{
 public class QueryTaglistsCommand : TaglistCommandsBase{
  public QueryTaglistsCommand(TaglistRepository Repository)
  :base(Repository){}

  [Command("Taglists")]
  [Summary("Displays a list of all Taglists.")]
  public async Task Execute(
  ){
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