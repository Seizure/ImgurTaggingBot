using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using Tagaroo.Model;
using Tagaroo.Imgur;
using Tagaroo.Discord;
using Tagaroo.DataAccess;
using Tagaroo.Logging;

using ImgurException=Imgur.API.ImgurException;

namespace Tagaroo.Application{
 /// <summary>
 /// Processes a Tag command, represented by a <see cref="TagCommandParameters"/> object,
 /// which indicates to alert all Imgur users in the Taglist specified in the command
 /// to the item that was Tagged,
 /// if they would be interested in the item according to other additional information about it
 /// also supplied in the Tag command.
 /// Specifically, this activity retrieves the <see cref="Taglist"/> identified in the Tag command via the associated <see cref="TaglistRepository"/>
 /// and calculates all the Imgur users that would be interested in the Tagged item with <see cref="Taglist.FilterByUsersInterestedIn"/>,
 /// using the additional details specified in the Tag command.
 /// It then alerts those users with <see cref="ImgurInterfacer.MentionUsers"/>.
 /// As well as this, details of the Tagged item,
 /// retrieved as a <see cref="GalleryItem"/> from the associated <see cref="ImgurInterfacer"/>,
 /// are posted to the relevant Archive Channel in the application's associated Discord Guild,
 /// via <see cref="DiscordInterfacer.PostGalleryItemDetails"/>.
 /// </summary>
 public class ProcessTagCommandActivity{
  protected readonly ImgurInterfacer Imgur;
  protected readonly DiscordInterfacer Discord;
  protected readonly TaglistRepository RepositoryTaglists;
  public ProcessTagCommandActivity(ImgurInterfacer Imgur,DiscordInterfacer Discord,TaglistRepository RepositoryTaglists){
   this.Imgur=Imgur;
   this.Discord=Discord;
   this.RepositoryTaglists=RepositoryTaglists;
  }

  /// <summary>
  /// <para>Preconditions: The associated <see cref="DiscordInterfacer"/> is in a Connected state</para>
  /// </summary>
  public async Task Execute(TagCommandParameters Command){
   Log.Application_.LogVerbose(
    "Processing Tag command for {2} '{0}', Tagged to Taglist '{1}'",
    Command.ItemID,Command.TaglistName,
    Command.isItemAlbum ? "Album" : "Image"
   );
   Task<GalleryItem> TaggedItemTask;
   if(!Command.isItemAlbum){
    TaggedItemTask=Imgur.ReadGalleryImage(Command.ItemID);
   }else{
    TaggedItemTask=Imgur.ReadGalleryAlbum(Command.ItemID);
   }
   Task<Taglist> LoadTaglistTask = RepositoryTaglists.Load(Command.TaglistName);
   Taglist SpecifiedTaglist;
   try{
    SpecifiedTaglist = await LoadTaglistTask;
    //Any exceptions from TaggedItemTask will go unobserved if an exception is thrown in this try block
   }catch(EntityNotFoundException){
    Log.Application_.LogWarning(
     "The Taglist named '{0}' does not exist, which was specified by a Tag command on the Imgur Gallery Item with ID '{1}' (Comment ID {2:D})",
     Command.TaglistName,Command.ItemID,Command.HostCommentID
    );
    return;
   }catch(DataAccessException Error){
    Log.Application_.LogError(
     "Error loading Taglist '{1}' while processing Tag command on the Imgur Gallery Item with ID '{0}'; processing of command aborted: {2}",
     Command.ItemID,Command.TaglistName,Error.Message
    );
    return;
   }
   Task MentionUsersTask = MentionInterestedUsers(Command,SpecifiedTaglist);
   GalleryItem TaggedItem;
   try{
    TaggedItem = await TaggedItemTask;
    //Any exceptions from MentionUsersTask will go unobserved if an exception is thrown in this try block
   }catch(ImgurException Error){
    Log.Application_.LogError("Error acquiring details for Tagged Imgur Gallery Item with ID '{0}'; unable to process Tag command for that item: {1}",Command.ItemID,Error.Message);
    return;
   }
   await Task.WhenAll(
    //Long-running operation, due to delay needed between Imgur Comments
    MentionUsersTask,
    ArchiveTaggedItem(Command,TaggedItem,SpecifiedTaglist)
   );
   Log.Application_.LogVerbose(
    "Completed processing of Tag command for {2} '{0}' to Taglist '{1}'",
    Command.ItemID,Command.TaglistName,
    Command.isItemAlbum ? "Album" : "Image"
   );
   await Imgur.LogRemainingBandwidth(TraceEventType.Verbose);
  }

  protected async Task MentionInterestedUsers(TagCommandParameters Command,Taglist SpecifiedTaglist){
   ISet<TaglistRegisteredUser> InterestedUsers = SpecifiedTaglist.FilterByUsersInterestedIn( Command.Rating, Command.Categories );
   Log.Application_.LogVerbose("Mentioning {0} total users in response to Tag command for item '{1}'",InterestedUsers.Count,Command.ItemID);
   ISet<string> InterestedUsernames=(
    from U in InterestedUsers
    select U.Username
   ).ToHashSet();
   try{
    await Imgur.MentionUsers(Command.ItemID, Command.HostCommentID, InterestedUsernames);
   }catch(ImgurException Error){
    Log.Application_.LogError(
     "Error Mentioning users in Taglist '{1}' in response to the Tag command on the Imgur Gallery Item with ID '{0}'; users may have been partially Mentioned, consider re-Tagging the Gallery Item: {2}",
     Command.ItemID,Command.TaglistName,Error.Message
    );
    return;
   }
  }

  protected async Task ArchiveTaggedItem(TagCommandParameters Command,GalleryItem ToArchive,Taglist SpecifiedTaglist){
   Log.Application_.LogVerbose("Archiving item '{0}' to relevant Archive Channel for Taglist '{1}'",ToArchive.ID,SpecifiedTaglist.Name);
   try{
    await Discord.PostGalleryItemDetails(
     SpecifiedTaglist.ArchiveChannelIDForRating(Command.Rating),
     ToArchive
    );
   }catch(DiscordException Error){
    Log.Application_.LogError(
     "Error archiving Imgur Gallery Item with ID '{0}' to the relevant Archive channel for Taglist '{1}': {2}",
     ToArchive.ID,SpecifiedTaglist.Name,Error.Message
    );
   }
  }
 }
}