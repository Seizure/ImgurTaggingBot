using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;

namespace Tagaroo.Model{
 /// <summary>
 /// Represents an Imgur Tag command on a particular Imgur Gallery Item,
 /// including details of the parameters of the command.
 /// </summary>
 public class Tag{
  /// <summary>
  /// The ID of the Imgur Comment that the command is in, as provided by the Imgur API.
  /// </summary>
  public int HostCommentID {get;}
  /// <summary>
  /// The ID of the Imgur Gallery Item that has been Tagged,
  /// in other words the ID of the Gallery Item that the Comment containing the command is for,
  /// as provided by the Imgur API.
  /// Non-null.
  /// </summary>
  public string ItemID {get;}
  /// <summary>
  /// true if the Gallery Item identified by <see cref="ItemID"/> is an Album,
  /// otherwise false, in which case it is an Image.
  /// </summary>
  public bool isItemAlbum {get;}
  /// <summary>
  /// The name of the Taglist with which to Tag the Gallery Item with,
  /// as provided in the Tag command.
  /// Normalized, non-null.
  /// </summary>
  public string TaglistName {get;}
  /// <summary>
  /// The Rating that the Gallery Item has been Tagged with,
  /// as provided in the Tag command.
  /// </summary>
  public Ratings Rating {get;}
  /// <summary>
  /// The set of Categories that the Gallery Item has been Tagged with,
  /// as provided in the Tag command.
  /// Normalized.
  /// </summary>
  public ISet<string> Categories {get;}

  public Tag(
   int HostCommentID,
   string ItemID,
   bool ItemAlbum,
   string TaglistName,
   Ratings Rating,
   ICollection<string> Categories
  ){
   if(ItemID is null){throw new ArgumentNullException(nameof(ItemID));}
   if(TaglistName is null){throw new ArgumentNullException(nameof(TaglistName));}
   this.HostCommentID=HostCommentID;
   this.ItemID=ItemID;
   this.isItemAlbum=ItemAlbum;
   this.TaglistName = TaglistName.Normalize(NormalizationForm.FormKD);
   this.Rating=Rating;
   this.Categories = (
    from C in Categories
    select C.Normalize(NormalizationForm.FormKD)
   ).ToImmutableHashSet();
  }
 }

 /// <summary>
 /// The Ratings that Gallery Items can be Tagged with.
 /// A Gallery Item can only be Tagged with one Rating (per Tag command).
 /// </summary>
 public enum Ratings{
  Explicit,
  Questionable,
  Safe
 }
}