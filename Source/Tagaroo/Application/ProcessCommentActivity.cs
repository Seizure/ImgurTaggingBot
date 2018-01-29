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

  public Task ExecuteIfNew(IComment Process){
   return CommandParser.ProcessCommands(Process,CommandHandler);
  }

  public Task ExecuteUnconditionally(IComment Process){
   return CommandParser.ProcessCommandsUnconditionally(Process,CommandHandler);
  }
 }

 internal class ProcessCommandActivityImgurCommandHandler : ImgurCommandHandler{
  protected readonly ProcessTagCommandActivity CommandHandler_Tag;
  public ProcessCommandActivityImgurCommandHandler(ProcessTagCommandActivity CommandHandler_Tag){
   this.CommandHandler_Tag=CommandHandler_Tag;
  }

  public Task ProcessTagCommand(Tag CommandParameter){
   return CommandHandler_Tag.Execute(CommandParameter);
  }
 }
}