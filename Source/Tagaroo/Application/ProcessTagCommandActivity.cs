using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using Tagaroo.Model;
using Tagaroo.Imgur;
using Tagaroo.Discord;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 public class ProcessTagCommandActivity{
  protected readonly ImgurInterfacer Imgur;
  protected readonly DiscordInterfacer Discord;
  protected readonly TaglistRepository RepositoryTaglists;
  public ProcessTagCommandActivity(ImgurInterfacer Imgur,DiscordInterfacer Discord,TaglistRepository RepositoryTaglists){
   this.Imgur=Imgur;
   this.Discord=Discord;
   this.RepositoryTaglists=RepositoryTaglists;
  }

  public async Task Execute(Tag Command){
   Task<GalleryItem> TaggedItemTask;
   if(!Command.isItemAlbum){
    TaggedItemTask=Imgur.ReadGalleryImage(Command.ItemID);
   }else{
    TaggedItemTask=Imgur.ReadGalleryAlbum(Command.ItemID);
   }
   Task<IReadOnlyDictionary<string,Taglist>> AllTaglistsTask = RepositoryTaglists.LoadAll();
   GalleryItem TaggedItem;
   try{
    TaggedItem = await TaggedItemTask;
   }catch(ImgurException Error){
    Log.Application_.LogError("Error acquiring details for Tagged Imgur Gallery item with ID '{0}': {1}",Command.ItemID,Error.Message);
    //Exceptions from AllTaglistsTask may go unobserved
    return;
   }
   IReadOnlyDictionary<string,Taglist> AllTaglists;
   try{
    AllTaglists = await AllTaglistsTask;
   }catch(DataAccessException Error){
    Log.Application_.LogError("Error loading Taglists while processing Tag command: "+Error.Message);
    return;
   }
   if( !AllTaglists.TryGetValue(Command.TaglistName,out Taglist SpecifiedTaglist) ){
    Log.Application_.LogWarning(
     "The Taglist named '{0}' does not exist, which was specified by a Tag command on the Imgur Gallery item with ID '{1}' (Comment ID {2:D})",
     Command.TaglistName,Command.ItemID,Command.HostCommentID
    );
    return;
   }
   await Task.WhenAll(
    MentionInterestedUsers(Command,SpecifiedTaglist),
    ArchiveTaggedItem(Command,TaggedItem,SpecifiedTaglist)
   );
  }

  protected async Task MentionInterestedUsers(Tag Command,Taglist SpecifiedTaglist){
   ISet<TaglistRegisteredUser> InterestedUsers = SpecifiedTaglist.FilterByUsersInterestedIn( Command.Rating, Command.Categories );
   ISet<string> InterestedUsernames=(
    from U in InterestedUsers
    select U.Username
   ).ToHashSet();
   try{
    await Imgur.MentionUsers(Command.ItemID, Command.HostCommentID, InterestedUsernames);
   }catch(ImgurException Error){
    Log.Application_.LogError(
     "Error Mentioning users in Taglist '{1}' in response to the Tag command on the Imgur Gallery item with ID '{0}': {2}",
     Command.ItemID,Command.TaglistName,Error.Message
    );
    return;
   }
  }

  protected async Task ArchiveTaggedItem(Tag Command,GalleryItem ToArchive,Taglist SpecifiedTaglist){
   try{
    await Discord.PostGalleryItemDetails(
     SpecifiedTaglist.ArchiveChannelIDForRating(Command.Rating),
     ToArchive
    );
   }catch(DiscordException Error){
    Log.Application_.LogError(
     "Error archiving Imgur Gallery item with ID '{0}' to the relevant Archive channel for Taglist '{1}': {2}",
     ToArchive.ID,SpecifiedTaglist.Name,Error.Message
    );
   }
  }
 }
}