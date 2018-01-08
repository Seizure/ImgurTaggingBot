using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using Imgur.API.Models;
using Tagaroo.Model;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Imgur{
 public class ImgurCommandParser{
  private readonly ImgurInterfacer Imgur;
  public ImgurCommandParser(ImgurInterfacer Imgur){
   this.Imgur=Imgur;
  }

  public async Task ProcessCommands(IComment HostComment,ImgurCommandHandler Callback){
   await ProcessCommands(HostComment,Callback,true);
  }
  public async Task ProcessCommandsUnconditionally(IComment HostComment,ImgurCommandHandler Callback){
   await ProcessCommands(HostComment,Callback,false);
  }

  protected async Task ProcessCommands(IComment HostComment,ImgurCommandHandler Callback,bool CheckForReplies){
   ICollection<Match> ParsedCommands = Pattern_Command.Matches(HostComment.CommentText);
   List<Task> Tasks=new List<Task>(ParsedCommands.Count);
   foreach(Match ParsedCommand in ParsedCommands){
    if(CheckForReplies){
     //Skip Comments that have already been replied to by this application; otherwise Comments will be re-processed
     if(!await ShouldProcess(HostComment)){
      continue;
     }
    }
    string Command = ParsedCommand.Groups[1].Value
     .Normalize(NormalizationForm.FormKD)
     .ToUpperInvariant()
    ;
    switch(Command){
    case Command_Tag:
    default:
     if(ParseTagCommand(
      HostComment.CommentText,
      ParsedCommand.Index,
      HostComment.Id,
      //The ImageId property will also be set if the Comment is on an Album, in which case it will be the Album ID
      HostComment.ImageId,
      HostComment.OnAlbum,
      out Tag CommandParameter
     )){
      Tasks.Add(Callback.ProcessTagCommand(CommandParameter));
     }else{
      Log.Imgur_.LogWarning(
       "The following Tag command on the Imgur Gallery item '{0}' by User '{1}' could not be parsed: {2}",
       HostComment.ImageId,HostComment.Author,HostComment.CommentText.Substring(ParsedCommand.Index)
      );
     }
    break;
    }
   }
   await Task.WhenAll(Tasks);
  }

  protected async Task<bool> ShouldProcess(IComment ToProcess){
   IEnumerable<IComment> Replies;
   //? Imgur seems inconsistent in whether or not Comment Replies are included
   if(ToProcess.Children.Count()>0){
    Replies = ToProcess.Children;
   }else{
    try{
     Replies = await Imgur.ReadCommentReplies(ToProcess);
    }catch(ImgurException Error){
     Log.Imgur_.LogError(
      "Unable to retrieve replies for Comment with ID {0:D} (by '{1}' on {2:u}) to determine if it has been processed; skipping Comment. Details: {3}",
      ToProcess.Id,ToProcess.Author,ToProcess.DateTime,Error.Message
     );
     return false;
    }
   }
   if(Replies.Any(
    C => Imgur.isCommentByThisApplication(C)
   )){
    return false;
   }
   return true;
  }

  public bool ParseTagCommand(string Input,int StartsAt,int HostCommentID,string OnItemID,bool ItemAlbum,out Tag Result){
   Result=null;
   Match ParsedTagCommand = Pattern_Tag.Match(Input,StartsAt);
   if(!ParsedTagCommand.Success){
    return false;
   }
   Result = new Tag(
    HostCommentID,
    OnItemID,
    ItemAlbum,
    ParsedTagCommand.Groups[1].Value,
    ParseRatingSpecifier(
     ParsedTagCommand.Groups[2].Success ? ParsedTagCommand.Groups[2].Value : null
    ),
    (
     from ParsedCategory in ParsedTagCommand.Groups[3].Captures
     where !string.IsNullOrEmpty(ParsedCategory.Value)
     select ParsedCategory.Value
    ).ToList()
   );
   return true;
  }

  protected Ratings ParseRatingSpecifier(string Specifier){
   if(Specifier!=null){
    Specifier=Specifier.Normalize(NormalizationForm.FormKD).ToUpperInvariant();
   }
   switch(Specifier){
    case RatingSpecifier_Safe:         return Ratings.Safe;
    case RatingSpecifier_Questionable: return Ratings.Questionable;
    case RatingSpecifier_Explicit:     return Ratings.Explicit;
    default:                           return DefaultRating;
   }
  }

  protected readonly Regex Pattern_Command=new Regex(
   /*
   The expression for matching the command specifier should be the same as for matching a Taglist name,
   as a Taglist name may appear instead of a command specifier
   as shorthand for the "tag" command.
   */
   CommandPrefix + @" (?>[\p{Zs}\t]+) (?>([a-z0-9_-]+))",
   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant
   |RegexOptions.IgnorePatternWhitespace|RegexOptions.Compiled
  );
  
  /*
  @Tagaroo [tag] <taglist-name> [<rating>] [ <category> <category> ... ]
  Whitespace-delimited; terminated by a new line or other unrecognized character.
  Optional elements should not be ommitted if they conflict with subsequent elements,
  such as a Taglist with the name "tag", or a Category "S".
  The command is case-insensitive except for <taglist-name> and <category>, which are case-sensitive.
  Permissible characters for <taglist-name> & <category> are A-Z, a-z, 0-9, '-', '_'.
  <rating> must be one of 'S', 'Q', 'E' (case-insensitive).
  
  Note:
  Take care when permitting additional characters in Category names;
  the expression will attempt to match as many Category names as possible,
  which may stray beyond what the tagger thinks would be the end of the command.
  */
  protected readonly Regex Pattern_Tag=new Regex(@"
#Prefix, 'tag' command optional
\G "+CommandPrefix+@" (?: (?>[\p{Zs}\t]+) tag )?
#Capture Taglist name
#If adding permissible characters to Taglist names, be sure to update the PatternCommand expression as well
(?>[\p{Zs}\t]+) (?>([a-z0-9_-]+))
#Capture Rating, optional
#If adding permissible characters to Category names, be sure to update the lookahead here as well
(?: (?>[\p{Zs}\t]+) ([SQE])(?![a-z0-9_-]) )?
#Capture Categories until unrecognized character
(?>(?:
(?>[\p{Zs}\t]+) (?>([a-z0-9_-]+))
)*)
",
   RegexOptions.IgnoreCase|RegexOptions.CultureInvariant|
   RegexOptions.IgnorePatternWhitespace|RegexOptions.Compiled
  );
  /*
  Test Inputs:
  @Tagaroo tag taglist-name
  @Tagaroo taglist-name
  @Tagaroo tag taglist-name S
  @Tagaroo taglist-name S
  @Tagaroo taglist-name S S-Category
  @Tagaroo taglist-name S-Category
  @Tagaroo taglist-name S-
  @Tagaroo taglist-name S; #Should match excluding semicolon
  @Tagaroo taglist-name S-Category S
  @Tagaroo S S S S
  @Tagaroo tag tag tag tag
  @Tagaroo tag tag S tag tag
  @Tagaroo tag
  */

  protected const string CommandPrefix = "@Tagaroo2";
  protected const string Command_Tag = "TAG";
  protected const string RatingSpecifier_Safe = "S";
  protected const string RatingSpecifier_Questionable = "Q";
  protected const string RatingSpecifier_Explicit = "E";
  protected readonly Ratings DefaultRating = Ratings.Explicit;
 }

 public interface ImgurCommandHandler{
  Task ProcessTagCommand(Tag CommandParameter);
 }
}