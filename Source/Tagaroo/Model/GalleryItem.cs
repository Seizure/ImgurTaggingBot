using System;
using System.Collections.Generic;
using System.Text;
using Imgur.API.Models;

namespace Tagaroo.Model{
 public class GalleryItem{
  public string ID {get;}
  public string Title {get;}
  public string Link {get;}
  public string AuthorUsername {get;}
  public string Description {get;}
  public bool NSFW {get;}
  public DateTimeOffset Created {get;}
  public string Categories {get;}
  public int? Width {get;}
  public int? Height {get;}

  public GalleryItem(
   string ID,
   string Title,
   string Link,
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
   this.Link = Link ?? string.Empty;
   this.AuthorUsername=AuthorUsername;
   this.Description=Description;
   this.NSFW=NSFW;
   this.Created=Created;
   this.Categories = Categories ?? string.Empty;
   this.Width=Width;
   this.Height=Height;
  }

  public bool hasTitle{get{
   return !(this.Title is null);
  }}
  public bool hasKnownAuthor{get{
   return !(this.AuthorUsername is null);
  }}
  public bool hasDescription{get{
   return !(this.Description is null);
  }}

  static public GalleryItem FromImgurImage(string ID,IImage ImgurGalleryItem){
   return new GalleryItem(
    ImgurGalleryItem.Id ?? ID,
    ImgurGalleryItem.Title,
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

  static public GalleryItem FromImgurAlbum(string ID,IAlbum ImgurGalleryItem){
   return new GalleryItem(
    ImgurGalleryItem.Id ?? ID,
    ImgurGalleryItem.Title,
    ImgurGalleryItem.Link,
    ImgurGalleryItem.AccountUrl,
    ImgurGalleryItem.Description,
    ImgurGalleryItem.Nsfw ?? NSFWIfNotSpecified,
    ImgurGalleryItem.DateTime,
    ImgurGalleryItem.Section,
    ImgurGalleryItem.CoverWidth,
    ImgurGalleryItem.CoverHeight
   );
  }

  public const bool NSFWIfNotSpecified = false;
 }
}