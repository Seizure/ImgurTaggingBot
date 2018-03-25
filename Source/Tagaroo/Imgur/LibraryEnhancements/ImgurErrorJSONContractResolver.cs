using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Tagaroo.Imgur.LibraryEnhancements{
 /*

 !!! Note !!!
 This solution may not work if an Imgur.API library version other than 4.0.1 is used.
 Take care to ensure this still works if using a different Imgur.API library version,
 or that the problem that this class addresses is no longer an issue.
 
 - Problem -
 The Imgur.API library (version 4.0.1 and likely other versions) uses the assembly-private class
 Imgur.API.Models.Impl.ImgurError to deserialize JSON error models returned by the Imgur API.
 This model assumes that the data.error property of the JSON model is always a string.
 This is not always the case.
 In particular, if too many calls are made to post Comments within a particular period of time,
 the API will return a structure equivalent to the following:
 {"data":{"error":{"code":2008,"message":"You're commenting too fast! Try again in 27 seconds","type":"Exception_CaptioningTooFast","exception":{"wait":27}},"request":"\/3\/comment","method":"POST"},"success":false,"status":429}
 As can be seen, the data.error property is not a string.
 This will cause an exception to be thrown from JsonConvert when the Imgur.API library attempts to deserialize such a response.

 - Solution -
 One way of solving this problem is implemented below.
 The Imgur.API library uses JsonConvert to deserialize most/all JSON responses from the Imgur API.
 Certain behaviors of JsonConvert can be overridden via the JsonConvert.DefaultSettings property.
 The following class implements IContractResolver,
 implementations of which are responsible for creating a model
 that specifies how .NET objects are mapped to and from JSON objects.
 The problematic member is the Error property of the ImgurError class in the Imgur.API library,
 which is specified as a string,
 yet sometimes the JSON returned by Imgur will be an object instead of a string.
 To solve this, we specify that the property's type is "object",
 and then convert from "object" to "string" when the .NET property is assigned to.
 
 */
 internal class ImgurErrorJSONContractResolver : DefaultContractResolver{
  public ImgurErrorJSONContractResolver():base(){}

  protected override JsonProperty CreateProperty(MemberInfo member,MemberSerialization memberSerialization){
   if(!(
    /*
    The problematic member that we need to override the deserialization behavior of
    is the Error property of the ImgurError class,
    which is assembly-private in the Imgur.API library
    */
    member.DeclaringType.FullName == "Imgur.API.Models.Impl.ImgurError"
    && member.Name == "Error"
    //&& member.MemberType == MemberTypes.Property
   )){
    return base.CreateProperty(member,memberSerialization);
   }
   JsonProperty Result = base.CreateProperty(member,memberSerialization);
   /*
   Override the property's detected type;
   make it "object" instead of "string" so any JSON type can be handled
   */
   Result.PropertyType = typeof(object);
   /*
   When the .NET property ImgurError.Error is assigned to,
   convert the read JSON "object" into "string"
   */
   Result.ValueProvider = new ObjectToStringValueSetter(Result.ValueProvider);
   return Result;
  }

  /*
  public override JsonContract ResolveContract(Type type){
   var _=base.ResolveContract(type);
   return _;
  }
  protected override JsonContract CreateContract(Type objectType){
   var _=base.CreateContract(objectType);
   return _;
  }
  protected override JsonObjectContract CreateObjectContract(Type objectType){
   var _=base.CreateObjectContract(objectType);
   return _;
  }
  protected override IList<JsonProperty> CreateProperties(Type type,MemberSerialization memberSerialization){
   var _=base.CreateProperties(type,memberSerialization);
   return _;
  }
  protected override IValueProvider CreateMemberValueProvider(MemberInfo member){
   var _=base.CreateMemberValueProvider(member);
   return _;
  }
  protected override string ResolvePropertyName(string propertyName){
   var _=base.ResolvePropertyName(propertyName);
   return _;
  }
  */
 }

 internal class ObjectToStringValueSetter : IValueProvider{
  protected readonly IValueProvider Decorate;
  public ObjectToStringValueSetter(IValueProvider Decorate){
   this.Decorate=Decorate;
  }
  
  public object GetValue(object target){
   return Decorate.GetValue(target);
  }
  public void SetValue(object target,object value){
   Decorate.SetValue(target,value?.ToString());
  }
 }
}