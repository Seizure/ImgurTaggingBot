using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Imgur.API.Models;
using Tagaroo.Imgur;
using Tagaroo.Model;

namespace Tagaroo.Application{
 public class ProcessCommentActivity{
  protected readonly ImgurCommandParser CommandParser;
  protected readonly ImgurCommandHandler CommandHandler;
  
  public ProcessCommentActivity(
   ImgurCommandParser CommandParser,
   ProcessTagCommandActivity CommandHandler_Tag
  ){
   this.CommandParser=CommandParser;
   this.CommandHandler = new ProcessCommandActivityImgurCommandHandler(
    CommandHandler_Tag
   );
  }

  public async Task ExecuteIfNew(IComment Process){
   await CommandParser.ProcessCommands(Process,CommandHandler);
  }

  public async Task ExecuteUnconditionally(IComment Process){
   await CommandParser.ProcessCommandsUnconditionally(Process,CommandHandler);
  }
 }

 internal class ProcessCommandActivityImgurCommandHandler : ImgurCommandHandler{
  protected readonly ProcessTagCommandActivity CommandHandler_Tag;
  public ProcessCommandActivityImgurCommandHandler(ProcessTagCommandActivity CommandHandler_Tag){
   this.CommandHandler_Tag=CommandHandler_Tag;
  }

  public async Task ProcessTagCommand(Tag CommandParameter){
   await CommandHandler_Tag.Execute(CommandParameter);
  }
 }
}