using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Tagaroo.Model;
using Tagaroo.DataAccess;

namespace Tagaroo.Discord.Commands{
 abstract public class TaglistCommandsBase : CommandBase{
  protected readonly TaglistRepository Repository;
  protected TaglistCommandsBase(TaglistRepository Repository):base(){
   this.Repository=Repository;
  }

  protected async Task<Taglist> ReadTaglist(string TaglistName){
   TaglistName = TaglistName.Normalize(NormalizationForm.FormKD);
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

  protected async Task<IReadOnlyCollection<Taglist>> ReadAllTaglists(){
   try{
    return await Repository.ReadAllHeaders();
   }catch(DataAccessException Error){
    await base.ReplyAsync(
     "Error retrieving Taglists: "+Error.Message
    );
    return null;
   }
  }

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