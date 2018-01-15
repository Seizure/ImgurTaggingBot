using System;
using Newtonsoft.Json;
using Imgur.API.Models;

namespace Tagaroo.Imgur.LibraryEnhancements{
 internal class ImgurError : IImgurError{
  
  [JsonProperty("error")]
  public string Error
  {
   get ;

   set ;
  }

  public string Method
  {
   get ;

   set ;
  }

  public string Request
  {
   get ;

   set ;
  }
 }
}
