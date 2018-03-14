using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Imgur.API.Models;
using Tagaroo.Imgur;
using Tagaroo.Model;
using Tagaroo.Logging;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class.
 /// Takes a particular Comment to process, and hands it off to an <see cref="ImgurCommandParser"/> to parse the Imgur commands it contains.
 /// For each command found, the relevant sub-activity is executed.
 /// This class explicitly implements the <see cref="ImgurCommandHandler"/> that the <see cref="ImgurCommandParser"/> hands off parsed commands to.
 /// </summary>
 public class ProcessCommentActivity : ImgurCommandHandler{
  protected readonly ImgurCommandParser CommandParser;
  protected readonly ProcessTagCommandActivity CommandHandler_Tag;
  
  public ProcessCommentActivity(
   ImgurCommandParser CommandParser,
   ProcessTagCommandActivity CommandHandler_Tag
  ){
   this.CommandParser=CommandParser;
   this.CommandHandler_Tag=CommandHandler_Tag;
  }

  /// <summary>
  /// <para>Preconditions: The associated <see cref="DiscordInterfacer"/> of the associated <see cref="ProcessTagCommandActivity"/> is in a Connected state</para>
  /// Executes <see cref="ImgurCommandParser.ProcessCommands"/>.
  /// </summary>
  public Task ExecuteIfNew(IComment Process){
   Log.Application_.LogVerbose("Processing commands in Comment made by '{1}' at {2:u} on Gallery Item '{3}' [#{0:D}]",Process.Id,Process.Author,Process.DateTime,Process.ImageId);
   return CommandParser.ProcessCommands(Process,this);
  }

  /// <summary>
  /// <para>Preconditions: The associated <see cref="DiscordInterfacer"/> of the associated <see cref="ProcessTagCommandActivity"/> is in a Connected state</para>
  /// Executes <see cref="ImgurCommandParser.ProcessCommandsUnconditionally"/>.
  /// </summary>
  public Task ExecuteUnconditionally(IComment Process){
   Log.Application_.LogVerbose("Processing commands unconditionally in Comment made by '{1}' at {2:u} on Gallery Item '{3}' [#{0:D}]",Process.Id,Process.Author,Process.DateTime,Process.ImageId);
   return CommandParser.ProcessCommandsUnconditionally(Process,this);
  }

  Task ImgurCommandHandler.ProcessTagCommand(Tag CommandParameter){
   return CommandHandler_Tag.Execute(CommandParameter);
  }
 }
}