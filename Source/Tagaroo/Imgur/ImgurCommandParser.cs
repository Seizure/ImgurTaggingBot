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
 /// <summary>
 /// Parses an Imgur Comment for commands to execute,
 /// checking to ensure commands in the Comment haven't already been handled,
 /// and hands off parsed commands to an associated <see cref="ImgurCommandHandler"/> for execution.
 /// To determine if commands in a Comment have already been executed at some point previously,
 /// the Replies to the Comment are looked at;
 /// if there are any direct Replies to the Comment from this application,
 /// specifically if <see cref="ImgurInterfacer.isCommentByThisApplication"/> returns true,
 /// then the commands in the Comment are deemed as already handled.
 /// As such, implementations of <see cref="ImgurCommandHandler"/> should Reply to Comments
 /// to indicate they have been processed, so they will not be executed again.
 /// It is possible for a single Comment to contain multiple commands,
 /// but only a single Reply to the Comment will be looked for when determining
 /// if the Comment has been processed or not.
 /// 
 /// <para>
 /// The syntax for all commands, assuming a prefix of "@Tagaroo", is:
 /// <code>@Tagaroo ‹command› …</code>
 /// Where ‹command› identifies the command to execute (case-insensitive).
 /// The rest of the syntax after ‹command› depends on the command.
 /// Syntax elements are whitespace-separated.
 /// </para>
 /// 
 /// <para>
 /// The syntax for the "tag" command, assuming a prefix of "@Tagaroo", is:
 /// <code>@Tagaroo [tag] ‹taglist-name› [‹rating›] [ ‹category› ‹category› … ]</code>
 /// The command is terminated by an unrecognized character, such as a new line, period, comma, or simply the end of the comment.
 /// Syntax elements are whitespace-separated.
 /// Optional elements should not be ommitted if they conflict with subsequent elements,
 /// such as a Taglist with the name "tag", or a Category "S".
 /// The command is case-insensitive except for ‹taglist-name› and ‹category›, which are case-sensitive.
 /// Permissible characters for ‹taglist-name› & ‹category› are A-Z, a-z, 0-9, '-', '_'.
 /// ‹rating› must be one of 'S', 'Q', 'E' (case-insensitive),
 /// and defaults to <see cref="DefaultRating"/> if not specified.
 /// </para>
 /// </summary>
 public class ImgurCommandParser{
  protected readonly ImgurInterfacer Imgur;
  
  /// <param name="CommandPrefix">The prefix that all commands in Imgur Comments must begin with, such as "@Tagaroo", not including trailing whitespace</param>
  /// <param name="Imgur">To reduce Imgur API bandwidth consumption, Replies to Comments are lazily loaded, only if needed, using this instance</param>
  public ImgurCommandParser(string CommandPrefix, ImgurInterfacer Imgur){
   string CommandPrefixEscaped = Regex.Escape(CommandPrefix);
   this.Imgur=Imgur;
   
   this.Pattern_Command=new Regex(
    /*
    The expression for matching the command specifier should be the same as for matching a Taglist name in Pattern_Tag,
    as a Taglist name may appear instead of a command specifier
    as shorthand for the "tag" command.
    Any unrecognized commands are treated as "tag" commands.
    */
    CommandPrefixEscaped + @" (?>[\p{Zs}\t]+) (?>([a-z0-9_-]+))",
    RegexOptions.IgnoreCase|RegexOptions.CultureInvariant
    |RegexOptions.IgnorePatternWhitespace|RegexOptions.Compiled
   );

   /*
   Take care when permitting additional characters in Category names;
   the expression will attempt to match as many Category names as possible,
   which may stray beyond what the tagger thinks would be the end of the command.
   */
   this.Pattern_Tag=new Regex(@"
#Prefix, 'tag' command optional
\G "+CommandPrefixEscaped+@" (?: (?>[\p{Zs}\t]+) tag )?
#Capture Taglist name
#If adding permissible characters to Taglist names, be sure to update the Pattern_Command expression as well
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
  
  }

  /// <summary>
  /// As for <see cref="ProcessCommandsUnconditionally"/>, except <see cref="ShouldProcess"/> is called for the Comment,
  /// to determine if it has already been processed.
  /// For efficiency, it is called only if needed, to preserve Imgur API bandwidth.
  /// </summary>
  public Task ProcessCommands(IComment HostComment,ImgurCommandHandler Callback){
   return ProcessCommands(HostComment,Callback,true);
  }
  
  /// <summary>
  /// Parses any commands contained in the Comment, which must begin with the prefix specified in the constructor.
  /// For each command detected, if any, the relevant Parse method is called,
  /// and then the relevant method of <paramref name="Callback"/> is called with the results.
  /// </summary>
  /// <param name="HostComment">The Imgur Comment to parse for commands</param>
  /// <param name="Callback">An object that handles execution of any commands found</param>
  public Task ProcessCommandsUnconditionally(IComment HostComment,ImgurCommandHandler Callback){
   return ProcessCommands(HostComment,Callback,false);
  }

  protected async Task ProcessCommands(IComment HostComment,ImgurCommandHandler Callback,bool CheckForReplies){
   ICollection<Match> ParsedCommands = Pattern_Command.Matches(HostComment.CommentText);
   Log.Imgur_.LogVerbose("Parsing Comment made by '{1}' at {2:u} on Gallery Item '{3}' [#{0:D}] for commands; total commands found - {4}",HostComment.Id,HostComment.Author,HostComment.DateTime,HostComment.ImageId,ParsedCommands.Count);
   List<Task> Tasks=new List<Task>(ParsedCommands.Count);
   foreach(Match ParsedCommand in ParsedCommands){
    if(CheckForReplies){
     //Only need to check once per Comment
     CheckForReplies=false;
     //Skip Comments that have already been replied to by this application; otherwise Comments will be re-processed
     Log.Imgur_.LogVerbose("Checking if Comment #{0:D} has already been processed",HostComment.Id);
     if(!await ShouldProcess(HostComment)){
      Log.Imgur_.LogVerbose("Comment #{0:D} already processed; skipping",HostComment.Id);
      continue;
     }
    }
    string Command = ParsedCommand.Groups[1].Value
     .Normalize(NormalizationForm.FormKD)
     .ToUpperInvariant()
    ;
    Log.Imgur_.LogVerbose("Found Imgur Comment command '{1}' in Comment #{0:D}",HostComment.Id,Command);
    switch(Command){
    case Command_Tag:
    default:
     Log.Imgur_.LogVerbose("Parsing 'tag' command in Comment #{0:D}",HostComment.Id);
     if(ParseTagCommand(
      HostComment.CommentText,
      ParsedCommand.Index,
      HostComment.Id,
      //The ImageId property will also be set if the Comment is on an Album, in which case it will be the Album ID
      HostComment.ImageId,
      HostComment.OnAlbum,
      out Tag CommandParameter
     )){
      Log.Imgur_.LogVerbose(
       "'tag' command in Comment #{0:D} parsed successfully: Taglist - '{1}', Rating - {2}, Total Categoties - {3}",
       HostComment.Id,
       CommandParameter.TaglistName,CommandParameter.Rating,CommandParameter.Categories.Count
      );
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

  /// <summary>
  /// Returns false if the supplied Imgur Comment has already been processed.
  /// This is determined by checking the direct Replies to the Comment,
  /// and seeing if any of them were made by the application,
  /// specifically if <see cref="ImgurInterfacer.isCommentByThisApplication"/> returns true.
  /// This method may call the Imgur API.
  /// </summary>
  protected async Task<bool> ShouldProcess(IComment ToProcess){
   IEnumerable<IComment> Replies;
   //? Imgur seems inconsistent in whether or not Comment Replies are included
   if(ToProcess.Children.Count()>0){
    Replies = ToProcess.Children;
   }else{
    Log.Imgur_.LogVerbose("No sub-Comments found in model of Comment #{0:D}; lazily loading sub-Comments",ToProcess.Id);
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

  /// <summary>
  /// Parses a "tag" command from somewhere within an input string,
  /// constructing a <see cref="Tag"/> object as a result to represent the parsed command.
  /// </summary>
  /// <param name="Input">The input string that contains the "tag" command</param>
  /// <param name="StartsAt">The position at which the "tag" command begins, which should be the offset at which the command prefix begins</param>
  /// <param name="HostCommentID">ID of the Imgur Comment the command is within</param>
  /// <param name="OnItemID">The ID of the Imgur Gallery Item that the Comment containing the command is for</param>
  /// <param name="ItemAlbum">Whether or not <paramref name="OnItemID"/> is an Album rather than an Image</param>
  /// <param name="Result">The parsed result, if the parse was successful</param>
  /// <returns>true if the parse suceeded and so <paramref name="Result"/> was set, otherwise false</returns>
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
   if(!string.IsNullOrEmpty(Specifier)){
    Specifier=Specifier.Normalize(NormalizationForm.FormKD).ToUpperInvariant();
   }
   switch(Specifier){
    case RatingSpecifier_Safe:         return Ratings.Safe;
    case RatingSpecifier_Questionable: return Ratings.Questionable;
    case RatingSpecifier_Explicit:     return Ratings.Explicit;
    default:                           return DefaultRating;
   }
  }

  protected readonly Regex Pattern_Command;
  
  protected readonly Regex Pattern_Tag;

  protected const string Command_Tag = "TAG";
  protected const string RatingSpecifier_Safe = "S";
  protected const string RatingSpecifier_Questionable = "Q";
  protected const string RatingSpecifier_Explicit = "E";
  protected readonly Ratings DefaultRating = Ratings.Explicit;
 }

 /// <summary>
 /// Contains one method for each possible Imgur Comment command.
 /// When passed to <see cref="ImgurCommandParser"/>,
 /// the relevant method will be called for each successfully parsed command.
 /// </summary>
 public interface ImgurCommandHandler{
  Task ProcessTagCommand(Tag CommandParameter);
 }
}