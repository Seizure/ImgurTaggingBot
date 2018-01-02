using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Linq;

namespace Tagaroo.Model{
 public class Tag{
  public int HostCommentID {get;}
  public string ItemID {get;}
  public bool isItemAlbum {get;}
  public string TaglistName {get;}
  public Ratings Rating {get;}
  public ISet<string> Categories {get;}

  public Tag(
   int HostCommentID,
   string ItemID,
   bool ItemAlbum,
   string TaglistName,
   Ratings Rating,
   ICollection<string> Categories
  ){
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

 public enum Ratings{
  Explicit,
  Questionable,
  Safe
 }
}