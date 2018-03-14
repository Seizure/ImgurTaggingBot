using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;

namespace Tagaroo.Model{
 /// <summary>
 /// A Taglist is a collection of Imgur users interested in a particular subject that the Taglist represents,
 /// the subject usually indicated by the Taglist's descriptive name.
 /// Each Taglist is associated with a few Text Channels in the Discord Guild associated with the application,
 /// one Channel for each possible <see cref="Ratings"/> value.
 /// These Channels serve as archives for Tagged items, where details of those items will be posted;
 /// which archive Channel the details go to depending on the Tagged Rating of the item.
 /// Each instance of the application manages zero or more Taglists,
 /// which are typically retrieved and updated with the <see cref="DataAccess.TaglistRepository"/>.
 /// </summary>
 public class Taglist{
  private readonly IDictionary<string,TaglistRegisteredUser> RegisteredUsersByName;
  private readonly IDictionary<int,TaglistRegisteredUser> RegisteredUsersByID;
  /// <summary>
  /// Unique identifying name for the Taglist,
  /// usually indicative of the subject of interest that the Taglist is for.
  /// Normalized.
  /// </summary>
  public string Name {get;}
  /// <summary>
  /// ID of the Text Channel in the application's Discord Guild
  /// where Tagged Imgur Gallery Items should be archived,
  /// for those Tagged with the <see cref="Ratings.Safe"/> Rating.
  /// </summary>
  public ulong ArchiveChannelIDSafe {get;}
  /// <summary>
  /// ID of the Text Channel in the application's Discord Guild
  /// where Tagged Imgur Gallery Items should be archived,
  /// for those Tagged with the <see cref="Ratings.Questionable"/> Rating.
  /// </summary>
  public ulong ArchiveChannelIDQuestionable {get;}
  /// <summary>
  /// ID of the Text Channel in the application's Discord Guild
  /// where Tagged Imgur Gallery Items should be archived,
  /// for those Tagged with the <see cref="Ratings.Explicit"/> Rating.
  /// </summary>
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

  /// <summary>
  /// Immutable / read only;
  /// use <see cref="RegisterUser"/> and <see cref="UnRegisterUser"/> to add and remove users to and from a Taglist.
  /// </summary>
  public ISet<TaglistRegisteredUser> RegisteredUsers{get{
   return RegisteredUsersByID.Values.ToImmutableHashSet();
  }}

  /// <summary>
  /// Calculates the subset of <see cref="RegisteredUsers"/> that would be interested in items Tagged to this Taglist
  /// with the specified <paramref name="Rating"/> and set of <paramref name="Categories"/>.
  /// </summary>
  /// <remarks>
  /// A user is interested in a Tagged item with a particular Rating and set of Categories,
  /// if they are interested in items with that Rating,
  /// and if none of the item's Categories are in their Category Blacklist.
  /// </remarks>
  public ISet<TaglistRegisteredUser> FilterByUsersInterestedIn(Ratings Rating,ISet<string> Categories){
   Categories = (
    from C in Categories
    select C.Normalize(NormalizationForm.FormKD)
   ).ToHashSet();
   return (
    from U in RegisteredUsers
    where U.AcceptsRating(Rating)
    && Categories.Intersect( U.CategoryBlacklist,StringComparer.OrdinalIgnoreCase ).Count() <= 0
    select U
   ).ToHashSet();
  }

  /// <summary>
  /// Returns the value of one of the ArchiveChannelID properties
  /// that corresponds to the supplied <see cref="Ratings"/> value.
  /// </summary>
  public ulong ArchiveChannelIDForRating(Ratings ForRating){
   switch(ForRating){
    case Ratings.Safe:         return ArchiveChannelIDSafe;
    case Ratings.Questionable: return ArchiveChannelIDQuestionable;
    case Ratings.Explicit:     return ArchiveChannelIDExplicit;
    default:throw new ArgumentException();
   }
  }

  /// <summary>
  /// Returns the <see cref="TaglistRegisteredUser"/> in <see cref="RegisteredUsers"/> with the specified identifying <paramref name="Username"/>.
  /// </summary>
  /// <returns>false if no <see cref="TaglistRegisteredUser"/> with the specified <paramref name="Username"/> exists in <see cref="RegisteredUsers"/></returns>
  public bool TryGetRegisteredUser(string Username,out TaglistRegisteredUser Result){
   return RegisteredUsersByName.TryGetValue(Username,out Result);
  }
  /// <summary>
  /// Returns the <see cref="TaglistRegisteredUser"/> in <see cref="RegisteredUsers"/> with the specified identifying <paramref name="UserID"/>.
  /// </summary>
  /// <returns>false if no <see cref="TaglistRegisteredUser"/> with the specified <paramref name="UserID"/> exists in <see cref="RegisteredUsers"/></returns>
  public bool TryGetRegisteredUser(int UserID,out TaglistRegisteredUser Result){
   return RegisteredUsersByID.TryGetValue(UserID,out Result);
  }

  public bool hasRegisteredUser(string Username){
   return RegisteredUsersByName.ContainsKey(Username);
  }
  public bool hasRegisteredUser(int UserID){
   return RegisteredUsersByID.ContainsKey(UserID);
  }

  /// <summary>
  /// Either adds the supplied <see cref="TaglistRegisteredUser"/> to <see cref="RegisteredUsers"/>,
  /// or replaces the existing <see cref="TaglistRegisteredUser"/>.
  /// </summary>
  /// <returns>true if <paramref name="ToRegister"/> was added to <see cref="RegisteredUsers"/>, false if <paramref name="ToRegister"/> replaced an existing entry</returns>
  /// <exception cref="AlreadyExistsException">
  /// A <see cref="TaglistRegisteredUser"/> with the specified <see cref="TaglistRegisteredUser.Username"/>
  /// already exists in <see cref="RegisteredUsers"/> with a different <see cref="TaglistRegisteredUser.ID"/>;
  /// in order to Register the new user, the username conflict must be resolved,
  /// such as by UnRegistering the old username.
  /// The exception message will contain further detail.
  /// </exception>
  public bool RegisterUser(TaglistRegisteredUser ToRegister){
   if(RegisteredUsersByName.TryGetValue( ToRegister.Username, out TaglistRegisteredUser ExistingUserByName )){
    if(ToRegister.ID != ExistingUserByName.ID){
     throw new AlreadyExistsException(string.Format(
      "The username '{0}' is already registered under a different user ID of {1:D}",
      ToRegister.Username,ExistingUserByName.ID
     ));
    }
   }
   string UsernameToRemove = ToRegister.Username;
   if(RegisteredUsersByID.TryGetValue( ToRegister.ID, out TaglistRegisteredUser ExistingUserByID )){
    if(!ToRegister.Username.Equals( ExistingUserByID.Username, StringComparison.Ordinal )){
     UsernameToRemove = ExistingUserByID.Username;
    }
   }
   bool Added = !RegisteredUsersByID.Remove(ToRegister.ID);
   RegisteredUsersByName.Remove(UsernameToRemove);
   RegisteredUsersByID.Add(ToRegister.ID,ToRegister);
   /*
   Should always succeed;
   If ToRegister.Username already exists in RegisteredUsersByName, then ToRegister.ID must also be the same as the existing item
   otherwise an AlreadyExistsException would have been thrown,
   meaning that ToRegister.Username must be the same as the existing Username,
   thus UsernameToRemove will equal ToRegister.Username
   */
   RegisteredUsersByName.Add(ToRegister.Username,ToRegister);
   return Added;
  }

  /// <summary>
  /// Removes a <see cref="TaglistRegisteredUser"/> from <see cref="RegisteredUsers"/>,
  /// by their <see cref="TaglistRegisteredUser.ID"/>.
  /// </summary>
  /// <returns>The removed <see cref="TaglistRegisteredUser"/>, or null if not found and so nothing was removed</returns>
  public TaglistRegisteredUser UnRegisterUser(int UserID){
   if(RegisteredUsersByID.TryGetValue(UserID,out TaglistRegisteredUser ToRemove)){
    Remove(ToRemove);
    return ToRemove;
   }
   return null;
  }
  /// <summary>
  /// As for <see cref="UnRegisterUser(int)"/>, but by <see cref="TaglistRegisteredUser.Username"/>.
  /// </summary>
  public TaglistRegisteredUser UnRegisterUser(string Username){
   if(RegisteredUsersByName.TryGetValue(Username,out TaglistRegisteredUser ToRemove)){
    Remove(ToRemove);
    return ToRemove;
   }
   return null;
  }
  private void Remove(TaglistRegisteredUser ToRemove){
   RegisteredUsersByID.Remove(ToRemove.ID);
   RegisteredUsersByName.Remove(ToRemove.Username);
  }
 }

 /// <summary>
 /// Component of <see cref="Taglist"/>;
 /// an Imgur user that appears within a particular Taglist,
 /// including details of their preferences in that Taglist.
 /// Instances are identified by their <see cref="ID"/>;
 /// Instances are also identified by their <see cref="Username"/>;
 /// two instances with the same <see cref="ID"/> but a different <see cref="Username"/> indicates that the Imgur user's username has changed;
 /// two instances with the same <see cref="Username"/> but a different <see cref="ID"/> is invalid,
 /// and may occur if an old username that was changed away from or deleted is recycled.
 /// </summary>
 public class TaglistRegisteredUser : IEquatable<TaglistRegisteredUser>{
  /// <summary>
  /// The identifying username of the Imgur user.
  /// It is possible for users to change their usernames, but all usernames must be unique.
  /// </summary>
  public string Username{get;}
  /// <summary>
  /// The identifying numeric ID of the Imgur user. This does not change.
  /// </summary>
  public int ID{get;}
  /// <summary>
  /// The <see cref="Ratings"/> that the user is interested in for items Tagged to the parent Taglist.
  /// A user can be interested in multiple <see cref="Ratings"/>.
  /// </summary>
  public RatingFlags AcceptedRatings{get;}
  /// <summary>
  /// Immutable / read only;
  /// A collection of Categories that the user has indicated they are not interested in for the parent Taglist.
  /// Normalized.
  /// </summary>
  public ISet<string> CategoryBlacklist{get;set;}
  
  public TaglistRegisteredUser(string Username,int ID,RatingFlags AcceptedRatings,ICollection<string> CategoryBlacklist){
   if(Username is null){throw new ArgumentNullException(nameof(Username));}
   this.Username = Username;
   this.ID=ID;
   this.AcceptedRatings=AcceptedRatings;
   this.CategoryBlacklist = (
    from C in CategoryBlacklist select C.Normalize(NormalizationForm.FormKD)
   ).ToImmutableHashSet();
  }

  /// <summary>
  /// Returns true if this user is interested in items Tagged to the parent Taglist with the supplied <see cref="Ratings"/> value.
  /// </summary>
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

  /// <summary>
  /// Mirror of <see cref="Ratings"/>, allowing multiple values to be specified.
  /// </summary>
  [Flags]
  public enum RatingFlags{
   None=0x00,
   Safe=0x01,
   Questionable=0x02,
   Explicit=0x04
  }
 }
}