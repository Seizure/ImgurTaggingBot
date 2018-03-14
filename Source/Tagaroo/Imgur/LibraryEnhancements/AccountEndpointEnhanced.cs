using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Models;
using Imgur.API.Models.Impl;

using IApiClient = Imgur.API.Authentication.IApiClient;
using ImgurException=Imgur.API.ImgurException;

/*
The Imgur.Net library does not provide a means to query accounts by numeric ID.
As such, code in this namespace extends the API to facilitate this feature.
The code is based heavily on the code in the Imgur.Net library,
with appropriate tweaks as needed.
*/

namespace Tagaroo.Imgur.LibraryEnhancements{
 internal class AccountEndpointEnhanced : AccountEndpoint{
  
  public AccountEndpointEnhanced(IApiClient APIClient)
  :base(APIClient){}

  //Note — The Rate Limit of the IApiClient won't be updated after this call
  public async Task<IAccount> GetAccountAsync(int AccountID){
   HttpRequestMessage Request = new HttpRequestMessage(
    HttpMethod.Get,
    string.Format("account/?account_id={0:D}",AccountID)
   );
   HttpResponseMessage Response = await base.HttpClient.SendAsync(Request);
   string ResponseMessage = await ProcessResponseGeneric(Response);
   return JsonConvert.DeserializeObject< Basic<Account> >( ResponseMessage ).Data;
  }

  protected async Task<string> ProcessResponseGeneric(HttpResponseMessage Response){
   string ResponseMessage = ( await Response.Content.ReadAsStringAsync() ).Trim();
   if(string.IsNullOrWhiteSpace(ResponseMessage)){
    throw new ImgurException(string.Format("No content in response; {0} {1}",(int)Response.StatusCode,Response.ReasonPhrase));
   }
   if(ResponseMessage.StartsWith("<")){
    throw new ImgurException(string.Format("Invalid content in response; {0} {1}",(int)Response.StatusCode,Response.ReasonPhrase));
   }
   if(ResponseMessage.StartsWith("{\"data\":{\"error\":")){
    throw DeserializeError(ResponseMessage);
   }
   Basic<object> ResponseModelGeneric = JsonConvert.DeserializeObject< Basic<object> >( ResponseMessage );
   if(!ResponseModelGeneric.Success){
    throw DeserializeError(ResponseMessage);
   }
   return ResponseMessage;
  }

  private ImgurException DeserializeError(string ResponseMessage){
   Basic<ImgurError> Error = JsonConvert.DeserializeObject< Basic<ImgurError> >( ResponseMessage );
   return new ImgurException(Error.Data.Error);
  }
 }
}