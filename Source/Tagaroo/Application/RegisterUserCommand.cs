using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Imgur.API.Models;
using Tagaroo.Model;
using Tagaroo.DataAccess;
using Tagaroo.Imgur;
using Tagaroo.Logging;

using DiscordCommandBase=Tagaroo.Discord.DiscordCommandBase;
using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 /// <summary>
 /// Application-layer activity class, executed by the Discord.Commands API; see <see cref="DiscordCommandBase"/>.
 /// Implements commands for Registering an Imgur user to a Taglist, using the <see cref="Taglist.RegisterUser"/> method.
 /// If the user already exists in the Taglist, their details are updated.
 /// The Imgur user to Register can be specified by either their username or account ID;
 /// the command will attempt to retrieve their account ID from Imgur if their username is specified,
 /// or their username if their account ID is specified,
 /// which involves calling the Imgur API.
 /// The <see cref="Taglist"/> to Register them to will be briefly locked during execution of this activity via <see cref="TaglistRepository.LoadAndLock"/>,
 /// and then persisted using <see cref="TaglistRepository.Save"/>.
 /// </summary>
 public class RegisterUserCommand : DiscordCommandBase{
  private readonly TaglistRepository Repository;
  private readonly ImgurInterfacer Imgur;
  public RegisterUserCommand(TaglistRepository Repository,ImgurInterfacer Imgur){
   this.Repository=Repository;
   this.Imgur=Imgur;
  }

  [Command("Register")]
  [Summary("Registers a User to a Taglist. The user will be added to the Taglist's Registered Users, or if the Registered User already exists, they will have their preferences updated.")]
  public Task Execute(
   [Summary("The username of the Imgur user to Register.")]
   string Username,
   [Summary("The name of the Taglist to Register the User to.")]
   string TaglistName,
   [Summary("A case insensitive sequence of 'S', 'Q', and 'E' characters specifying the Ratings that the User is interested in.")]
   string RatingsInterestedInSpecifier,
   [Summary("A whitespace separated list of Categories that will form the User's Category Blacklist.")]
   params string[] CategoryBlacklist
  ){
   Log.Application_.LogVerbose("Executing 'Register' command for username '{0}', to Taglist '{1}' with Ratings {2}",Username,TaglistName,RatingsInterestedInSpecifier);
   return Execute(Username,null,TaglistName,RatingsInterestedInSpecifier,CategoryBlacklist);
  }

  [Command("Register")]
  public Task Execute(
   [Summary("The numeric account ID of the Imgur user to Register.")]
   int UserID,
   string TaglistName,
   string RatingsInterestedInSpecifier,
   params string[] CategoryBlacklist
  ){
   Log.Application_.LogVerbose("Executing 'Register' command for user ID '{0:D}', to Taglist '{1}' with Ratings {2}",UserID,TaglistName,RatingsInterestedInSpecifier);
   return Execute(null,UserID,TaglistName,RatingsInterestedInSpecifier,CategoryBlacklist);
  }

  private async Task Execute(
   string Username,
   int? UserID,
   string TaglistName,
   string RatingsInterestedInSpecifier,
   params string[] CategoryBlacklist
  ){
   if(UserID is null){
    if(string.IsNullOrWhiteSpace(Username)){
     await base.ReplyAsync("Specify a non-empty username");
     return;
    }
   }
   TaglistRegisteredUser.RatingFlags? ToRegisterRatings = ParseRatings(RatingsInterestedInSpecifier, out string RatingsParseError);
   if(ToRegisterRatings is null){
    await base.ReplyAsync(RatingsParseError);
    return;
   }
   Log.Application_.LogVerbose("Registering user; parsed Ratings - {0}",ToRegisterRatings);
   Tuple<int,string> ToRegisterUserDetails = await RetrieveUserIdentity(UserID,Username);
   if(ToRegisterUserDetails is null){return;}
   Log.Application_.LogVerbose("Registering user; username '{1}', ID {0:D}",ToRegisterUserDetails.Item1,ToRegisterUserDetails.Item2);
   TaglistRegisteredUser ToRegister = new TaglistRegisteredUser(
    ToRegisterUserDetails.Item2,
    ToRegisterUserDetails.Item1,
    ToRegisterRatings.Value,
    CategoryBlacklist
   );
   string Response = await ExecuteRegistration(TaglistName, ToRegister);
   await base.ReplyAsync(Response);
  }

  private TaglistRegisteredUser.RatingFlags? ParseRatings(string RatingsInterestedInSpecifier,out string ErrorResponse){
   ErrorResponse=null;
   if(string.IsNullOrEmpty(RatingsInterestedInSpecifier)){
    return TaglistRegisteredUser.RatingFlags.None;
   }
   RatingsInterestedInSpecifier = RatingsInterestedInSpecifier.Trim();
   TaglistRegisteredUser.RatingFlags Result = TaglistRegisteredUser.RatingFlags.None;
   foreach(char RatingSpecifier in RatingsInterestedInSpecifier){
    if(
     char.IsWhiteSpace(RatingSpecifier)
     ||char.IsControl(RatingSpecifier)
    ){
     continue;
    }
    switch(char.ToUpperInvariant(RatingSpecifier)){
     case 'S': Result |= TaglistRegisteredUser.RatingFlags.Safe; break;
     case 'Q': Result |= TaglistRegisteredUser.RatingFlags.Questionable; break;
     case 'E': Result |= TaglistRegisteredUser.RatingFlags.Explicit; break;
     default:
     ErrorResponse = string.Format(
      "'{0}' is not a valid Rating specifier; specify some combination of 'S' for Safe, 'Q' for Questionable, and 'E' for Explicit",
      RatingSpecifier
     );
     return null;
    }
   }
   return Result;
  }

  private async Task<Tuple<int,string>> RetrieveUserIdentity(int? UserID,string Username){
   if(UserID is null&&string.IsNullOrWhiteSpace(Username)){throw new ArgumentNullException();}
   IAccount UserDetails;
   try{
    if(UserID is null){
     UserDetails = await Imgur.ReadUserDetails(Username);
    }else{
     UserDetails = await Imgur.ReadUserDetails(UserID.Value);
    }
   }catch(ImgurException Error){
    if(UserID is null){
     await base.ReplyAsync(string.Format(
      "Could not retrieve details for Imgur user with username '{0}': ",
      Username
     ) + Error.Message);
    }else{
     await base.ReplyAsync(string.Format(
      "Could not retrieve details for Imgur user with account ID '{0:D}': ",
      UserID
     ) + Error.Message);
    }
    return null;
   }
   return new Tuple<int,string>(UserDetails.Id, UserDetails.Url);
  }

  private async Task<string> ExecuteRegistration(string TaglistName,TaglistRegisteredUser ToRegister){
   Log.Application_.LogVerbose("Registering user; performing Registration");
   Taglist Model;
   Lock Lock;
   try{
    Tuple<Taglist,Lock> LoadResult = await Repository.LoadAndLock(TaglistName);
    Model=LoadResult.Item1;
    Lock=LoadResult.Item2;
   }catch(EntityNotFoundException){
    return string.Format("No Taglist with the name '{0}' exists",TaglistName);
   }catch(DataAccessException Error){
    return "Error retrieving Taglist data: "+Error.Message;
   }
   try{
    bool added;
    try{
     added = Model.RegisterUser(ToRegister);
    }catch(AlreadyExistsException Error){
     return "Username Conflict — "+Error.Message;
    }
    try{
     await Repository.Save(Model,Lock);
    }catch(DataAccessException Error){
     return "Error saving updated Taglist: "+Error.Message;
    }
    string Response;
    if( added ){
     Response = "Imgur user '{1}' (ID {0:D}) has been Registered to Taglist '{2}'; Ratings — {3}; Category Blacklist — {4}";
    }else{
     Response = "Imgur user '{1}' (ID {0:D}) has been updated in the Taglist '{2}'; Updated Ratings — {3}; Updated Category Blacklist — {4}";
    }
    return string.Format(
     Response,
     ToRegister.ID,
     ToRegister.Username,
     Model.Name,
     ToRegister.AcceptedRatings,
     string.Join(", ",ToRegister.CategoryBlacklist)
    );
   }finally{
    Lock.Release();
   }
  }
 }
}