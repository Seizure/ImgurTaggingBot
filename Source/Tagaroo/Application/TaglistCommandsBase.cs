using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Tagaroo.Model;
using Tagaroo.DataAccess;

using DiscordCommandBase=Tagaroo.Discord.DiscordCommandBase;

namespace Tagaroo.Application{
 /// <summary>
 /// Base class for application-layer Discord command classes that deal with Taglists,
 /// containing some common code.
 /// </summary>
 abstract public class TaglistCommandsBase : DiscordCommandBase{
  protected readonly TaglistRepository Repository;
  protected TaglistCommandsBase(TaglistRepository Repository):base(){
   this.Repository=Repository;
  }

  /// <summary>
  /// Convenience method that calls <see cref="TaglistRepository.Load"/>,
  /// sending an error message and returning null upon any problem.
  /// </summary>
  protected async Task<Taglist> ReadTaglist(string TaglistName){
   try{
    return await Repository.Load(TaglistName);
   }catch(EntityNotFoundException){
    await base.ReplyAsync(string.Format(
     "No Taglist exists with the name '{0}'",
     TaglistName
    ));
    return null;
   }catch(DataAccessException Error){
    await base.ReplyAsync(
     "Error retrieving Taglist data: "+Error.Message
    );
    return null;
   }
  }

  /// <summary>
  /// Convenience method that calls <see cref="TaglistRepository.ReadAllHeaders"/>,
  /// sending an error message and returning null upon any problem.
  /// </summary>
  protected async Task<IReadOnlyCollection<Taglist>> ReadAllTaglists(){
   try{
    return (await Repository.ReadAllHeaders()).Values.ToList();
   }catch(DataAccessException Error){
    await base.ReplyAsync(
     "Error retrieving Taglists: "+Error.Message
    );
    return null;
   }
  }

  /// <summary>
  /// Convenience method that calls <see cref="TaglistRepository.LoadAll"/>,
  /// sending an error message and returning null upon any problem.
  /// </summary>
  protected async Task<IReadOnlyCollection<Taglist>> ReadAllTaglistsAndUsers(){
   try{
    return (await Repository.LoadAll()).Values.ToList();
   }catch(DataAccessException Error){
    await base.ReplyAsync(
     "Error retrieving Taglists: "+Error.Message
    );
    return null;
   }
  }

  /// <summary>
  /// Returns <see cref="Ratings.Safe"/> if <paramref name="RatingCharacter"/> is 'S',
  /// <see cref="Ratings.Questionable"/> if <paramref name="RatingCharacter"/> is 'Q',
  /// or <see cref="Ratings.Explicit"/> if <paramref name="RatingCharacter"/> is 'E',
  /// treating <paramref name="RatingCharacter"/> as case-insensitive.
  /// Sends an error message and returns null upon any problem.
  /// </summary>
  protected async Task<Ratings?> ParseRating(char RatingCharacter){
   RatingCharacter = char.ToUpperInvariant(RatingCharacter);
   switch(RatingCharacter){
    case 'S': return Ratings.Safe;
    case 'Q': return Ratings.Questionable;
    case 'E': return Ratings.Explicit;
   }
   await base.ReplyAsync(string.Format(
    "'{0}' is not a valid Rating specifier; specify 'S', 'Q', or 'E' to specify a Rating",
    RatingCharacter
   ));
   return null;
  }
 }
}