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
   this.ID=ID;
   this.Title=Title;
   this.Link=Link;
   this.AuthorUsername=AuthorUsername;
   this.Description=Description;
   this.NSFW=NSFW;
   this.Created=Created;
   this.Categories=Categories;
   this.Width=Width;
   this.Height=Height;
  }

  public bool hasKnownAuthor{get{
   return !(this.AuthorUsername is null);
  }}

  static public GalleryItem FromImgurImage(IImage ImgurGalleryItem){
   return new GalleryItem(
    ImgurGalleryItem.Id,
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

  static public GalleryItem FromImgurAlbum(IAlbum ImgurGalleryItem){
   return new GalleryItem(
    ImgurGalleryItem.Id,
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