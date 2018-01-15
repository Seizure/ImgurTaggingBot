using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;

namespace Tagaroo.Model{
 public class Taglist{
  public string Name {get;}
  public ISet<TaglistRegisteredUser> RegisteredUsers {get;}
  public ulong ArchiveChannelIDSafe {get;}
  public ulong ArchiveChannelIDQuestionable {get;}
  public ulong ArchiveChannelIDExplicit {get;}
  
  public Taglist(
   string Name,
   ulong ArchiveChannelIDSafe,
   ulong ArchiveChannelIDQuestionable,
   ulong ArchiveChannelIDExplicit,
   ImmutableHashSet<TaglistRegisteredUser> RegisteredUsers
  ){
   this.Name = Name.Normalize(NormalizationForm.FormKD);
   this.ArchiveChannelIDSafe=ArchiveChannelIDSafe;
   this.ArchiveChannelIDQuestionable=ArchiveChannelIDQuestionable;
   this.ArchiveChannelIDExplicit=ArchiveChannelIDExplicit;
   this.RegisteredUsers=RegisteredUsers;
  }

  public ISet<string> FilterByUsersInterestedIn(Ratings Rating,ISet<string> Categories){
   /*
   A user is interested in a Tagged item with a particular Rating and set of Categories,
   if they are interested in items with that Rating,
   and if none of the item's Categories are in their Category Blacklist.
   */
   return (
    from U in RegisteredUsers
    where U.AcceptsRating(Rating)
    && Categories.Intersect( U.CategoryBlacklist,StringComparer.OrdinalIgnoreCase ).Count() <= 0
    select U.Username
   ).ToHashSet();
  }

  public ulong ArchiveChannelIDForRating(Ratings ForRating){
   switch(ForRating){
    case Ratings.Safe:         return ArchiveChannelIDSafe;
    case Ratings.Questionable: return ArchiveChannelIDQuestionable;
    case Ratings.Explicit:     return ArchiveChannelIDExplicit;
    default:throw new ArgumentException();
   }
  }
 }

 public class TaglistRegisteredUser : IEquatable<TaglistRegisteredUser>{
  public string Username{get;}
  public int ID{get;}
  public RatingFlags AcceptedRatings{get;}
  public ISet<string> CategoryBlacklist{get;set;}
  
  public TaglistRegisteredUser(string Username,int ID,RatingFlags AcceptedRatings,ICollection<string> CategoryBlacklist){
   this.Username = Username.Normalize(NormalizationForm.FormKD);
   this.ID=ID;
   this.AcceptedRatings=AcceptedRatings;
   this.CategoryBlacklist = (
    from C in CategoryBlacklist select C.Normalize(NormalizationForm.FormKD)
   ).ToImmutableHashSet();
  }

  public bool AcceptsRating(Ratings operand){
   switch(operand){
    case Ratings.Safe:         return (this.AcceptedRatings&RatingFlags.Safe)!=0;
    case Ratings.Questionable: return (this.AcceptedRatings&RatingFlags.Questionable)!=0;
    case Ratings.Explicit:     return (this.AcceptedRatings&RatingFlags.Explicit)!=0;
    default:throw new ArgumentException();
   }
  }

  public override bool Equals(object operand){
   return this.Equals(operand as TaglistRegisteredUser);
  }
  public bool Equals(TaglistRegisteredUser operand){
   if(operand is null){return false;}
   return this.Username.Equals(operand.Username,StringComparison.Ordinal);
  }
  public override int GetHashCode(){
   return this.Username.GetHashCode();
  }

  [Flags]
  public enum RatingFlags{
   None=0x00,
   Safe=0x01,
   Questionable=0x02,
   Explicit=0x04
  }
 }
}