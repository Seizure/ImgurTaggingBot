using System;
using System.Collections.Generic;
using System.Text;
using Imgur.API.Models;

namespace Tagaroo.Model{
 /// <summary>
 /// Adapter for <see cref="IImage"/> and <see cref="IAlbum"/>,
 /// allowing them to be treated uniformly;
 /// the two static Factory Methods allow creation of instances from these types.
 /// Taggable items in Imgur's Gallery can be either standalone Images,
 /// or collections of Images known as Albums.
 /// For Albums, the representative image of the object will be the Album's Cover Image.
 /// </summary>
 public class GalleryItem{
  /// <summary>
  /// Always present.
  /// </summary>
  public string ID {get;}
  /// <summary>
  /// null if <see cref="hasTitle"/> is false.
  /// </summary>
  public string Title {get;}
  /// <summary>
  /// URL to the Gallery Item's web page.
  /// Non-null and may be empty; this information is returned from the Imgur API, and may not be a valid URI.
  /// </summary>
  public string LinkPage {get;}
  /// <summary>
  /// URL to the image resource of the Gallery Item's representative image;
  /// for Albums this will be the Album's Cover Image.
  /// Non-null and may be empty; will also not be present for Albums if the Cover Image was not supplied;
  /// this information is returned from the Imgur API, and may not be a valid URI.
  /// </summary>
  public string LinkImage {get;}
  /// <summary>
  /// null if <see cref="hasKnownAuthor"/> is false.
  /// </summary>
  public string AuthorUsername {get;}
  /// <summary>
  /// null if <see cref="hasDescription"/> is false.
  /// </summary>
  public string Description {get;}
  /// <summary>
  /// true if the Gallery Item was marked as NSFW, false if it was marked otherwise,
  /// and <see cref="NSFWIfNotSpecified"/> if no information was given.
  /// </summary>
  public bool NSFW {get;}
  public DateTimeOffset Created {get;}
  /// <summary>
  /// Non-null and may be empty.
  /// </summary>
  public string Categories {get;}
  /// <summary>
  /// The dimensions of the Gallery Item's representative image,
  /// if width information is available.
  /// </summary>
  public int? Width {get;}
  /// <summary>
  /// The dimensions of the Gallery Item's representative image,
  /// if height information is available.
  /// </summary>
  public int? Height {get;}

  protected GalleryItem(
   string ID,
   string Title,
   string LinkPage,
   string LinkImage,
   string AuthorUsername,
   string Description,
   bool NSFW,
   DateTimeOffset Created,
   string Categories,
   int? Width,
   int? Height
  ){
   if(ID is null){throw new ArgumentNullException(nameof(ID));}
   this.ID=ID;
   this.Title=Title;
   this.LinkPage = LinkPage ?? string.Empty;
   this.LinkImage = LinkImage ?? string.Empty;
   this.AuthorUsername=AuthorUsername;
   this.Description=Description;
   this.NSFW=NSFW;
   this.Created=Created;
   this.Categories = Categories ?? string.Empty;
   this.Width=Width;
   this.Height=Height;
  }

  /// <summary>
  /// false if the Imgur API didn't return Title data, which probably shouldn't happen,
  /// in which case <see cref="Title"/> will be null.
  /// </summary>
  public bool hasTitle{get{
   return !(this.Title is null);
  }}
  /// <summary>
  /// true if there is information about the Author of a Gallery Item,
  /// otherwise false in which case <see cref="AuthorUsername"/> will be null.
  /// </summary>
  public bool hasKnownAuthor{get{
   return !(this.AuthorUsername is null);
  }}
  /// <summary>
  /// false if the Imgur API didn't return Description data,
  /// in which case <see cref="Description"/> will be null.
  /// </summary>
  public bool hasDescription{get{
   return !(this.Description is null);
  }}
  /// <summary>
  /// true if both <see cref="Width"/> and <see cref="Height"/> are present.
  /// </summary>
  public bool hasDimensionInformation{get{
   return this.Width.HasValue && this.Height.HasValue;
  }}

  static public GalleryItem FromImgurImage(string ID,IImage ImgurGalleryItem){
   string EscapedID;
   try{
    EscapedID = Uri.EscapeDataString(ID);
   }catch(FormatException){
    EscapedID = ID;
   }
   return new GalleryItem(
    ImgurGalleryItem.Id ?? ID,
    ImgurGalleryItem.Title,
    string.Format(ImagePageURLFormat, EscapedID),
    ImgurGalleryItem.Link,
    null,
    ImgurGalleryItem.Description,
    ImgurGalleryItem.Nsfw ?? NSFWIfNotSpecified,
    ImgurGalleryItem.DateTime,
    ImgurGalleryItem.Section,
    ImgurGalleryItem.Width,
    ImgurGalleryItem.Height
   );
  }

  static public GalleryItem FromImgurAlbum(string ID,IAlbum ImgurGalleryItem,IImage AlbumCoverImage=null){
   return new GalleryItem(
    ImgurGalleryItem.Id ?? ID,
    ImgurGalleryItem.Title,
    ImgurGalleryItem.Link,
    AlbumCoverImage?.Link,
    ImgurGalleryItem.AccountUrl,
    ImgurGalleryItem.Description,
    ImgurGalleryItem.Nsfw ?? NSFWIfNotSpecified,
    ImgurGalleryItem.DateTime,
    ImgurGalleryItem.Section,
    ImgurGalleryItem.CoverWidth,
    ImgurGalleryItem.CoverHeight
   );
  }

  protected const string ImagePageURLFormat = "https://imgur.com/{0}";
  public const bool NSFWIfNotSpecified = false;
 }
}