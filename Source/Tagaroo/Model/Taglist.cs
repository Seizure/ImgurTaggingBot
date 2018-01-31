using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;

namespace Tagaroo.Model{
 public class Taglist{
  private readonly IDictionary<string,TaglistRegisteredUser> RegisteredUsersByName;
  private readonly IDictionary<int,TaglistRegisteredUser> RegisteredUsersByID;
  public string Name {get;}
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
   this.RegisteredUsersByID=new Dictionary<int,TaglistRegisteredUser>(
    from U in RegisteredUsers
    select new KeyValuePair<int,TaglistRegisteredUser>(U.ID, U)
   );
   this.RegisteredUsersByName=new Dictionary<string,TaglistRegisteredUser>(
    from U in RegisteredUsers
    select new KeyValuePair<string,TaglistRegisteredUser>(U.Username, U)
   );
  }

  public ISet<TaglistRegisteredUser> RegisteredUsers{get{
   return RegisteredUsersByID.Values.ToImmutableHashSet();
  }}

  public ISet<TaglistRegisteredUser> FilterByUsersInterestedIn(Ratings Rating,ISet<string> Categories){
   /*
   A user is interested in a Tagged item with a particular Rating and set of Categories,
   if they are interested in items with that Rating,
   and if none of the item's Categories are in their Category Blacklist.
   */
   return (
    from U in RegisteredUsers
    where U.AcceptsRating(Rating)
    && Categories.Intersect( U.CategoryBlacklist,StringComparer.OrdinalIgnoreCase ).Count() <= 0
    select U
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

  public bool TryGetRegisteredUser(string Username,out TaglistRegisteredUser Result){
   return RegisteredUsersByName.TryGetValue(Username,out Result);
  }
  public bool TryGetRegisteredUser(int UserID,out TaglistRegisteredUser Result){
   return RegisteredUsersByID.TryGetValue(UserID,out Result);
  }

  public bool hasRegisteredUser(string Username){
   return RegisteredUsersByName.ContainsKey(Username);
  }
  public bool hasRegisteredUser(int UserID){
   return RegisteredUsersByID.ContainsKey(UserID);
  }

  /// <exception cref="AlreadyExistsException"/>
  public bool RegisterUser(TaglistRegisteredUser ToRegister){
   if(RegisteredUsersByName.TryGetValue( ToRegister.Username, out TaglistRegisteredUser ExistingUser )){
    if(ToRegister.ID != ExistingUser.ID){
     throw new AlreadyExistsException(string.Format(
      "The username '{0}' is already registered under a different user ID of {1:D}",
      ToRegister.Username,ExistingUser.ID
     ));
    }
   }
   bool Added = !RegisteredUsersByID.Remove(ToRegister.ID);
   RegisteredUsersByName.Remove(ToRegister.Username);
   RegisteredUsersByID.Add(ToRegister.ID,ToRegister);
   RegisteredUsersByName.Add(ToRegister.Username,ToRegister);
   return Added;
  }

  public bool UnRegisterUser(int UserID){
   if(RegisteredUsersByID.TryGetValue(UserID,out TaglistRegisteredUser ToRemove)){
    Remove(ToRemove);
    return true;
   }
   return false;
  }
  public bool UnRegisterUser(string Username){
   if(RegisteredUsersByName.TryGetValue(Username,out TaglistRegisteredUser ToRemove)){
    Remove(ToRemove);
    return true;
   }
   return false;
  }
  private void Remove(TaglistRegisteredUser ToRemove){
   RegisteredUsersByID.Remove(ToRemove.ID);
   RegisteredUsersByName.Remove(ToRemove.Username);
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
   return this.ID.Equals(operand.ID);
  }
  public override int GetHashCode(){
   return this.ID.GetHashCode();
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