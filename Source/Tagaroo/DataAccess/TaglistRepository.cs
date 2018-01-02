using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.IO;
using Tagaroo.Model;

namespace Tagaroo.DataAccess{
 
 public interface TaglistRepository{
  void Initialize();

  /// <exception cref="DataAccessException"/>
  Task<IReadOnlyDictionary<string,Taglist>> LoadAll();

  /// <exception cref="DataAccessException"/>
  Task SaveAll(ICollection<Taglist> Save);
 }
 
 public class TaglistRepositoryMain : TaglistRepository{
  private readonly XMLFileRepository XMLDataFileHandler;
  private readonly string DataFilePath;
  private readonly bool Cache;
  private IReadOnlyDictionary<string,Taglist> CachedTaglists=null;
  
  public TaglistRepositoryMain(string DataFilePath,bool Cache){
   this.XMLDataFileHandler = new XMLFileRepository(DataFilePath,xmlns,"Tagaroo.DataAccess.Taglists.xsd");
   this.DataFilePath=DataFilePath;
   this.Cache=Cache;
  }

  //TODO Call on startup
  public void Initialize(){
   Initialize();
  }
  
  /*
  The following types can be converted to XML SimpleTypes,
  and as such are valid Content for XElement/XAttribute objects:
  <//docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/valid-content-of-xelement-and-xdocument-objects3>
  - string
  - bool
  - float
  - double
  - decimal
  - DateTime
  - DateTimeOffset
  - TimeSpan
  ToString is called for other types; IEnumerable types have their constituent elements added
  */
  /*
  The following types can be converted to from XML SimpleTypes,
  such as XAttribute objects or XElement with Simple Content:
  <//docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/how-to-retrieve-the-value-of-an-element-linq-to-xml>
  - Nullable<T> of any of the below
  - string
  - bool
  - int
  - uint
  - long
  - ulong
  - float
  - double
  - decimal
  - DateTime
  - TimeSpan
  - GUID
  */

  public async Task<IReadOnlyDictionary<string,Taglist>> LoadAll(){
   if(Cache && CachedTaglists!=null){
    return CachedTaglists;
   }
   IReadOnlyDictionary<string,Taglist> Result=await _LoadAll();
   this.CachedTaglists=Result;
   return Result;
  }
  
  /// <exception cref="DataAccessException"/>
  protected async Task<IReadOnlyDictionary<string,Taglist>> _LoadAll(){
   Dictionary<string,Taglist> Results;
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read);
   IEnumerable<Taglist> ResultsSource=
    from TL in DataDocument.Root.Elements(xmlns+"Taglist")
    select new Taglist(
     (string)TL.Attribute("Name"),
     (ulong)TL.Attribute("SafeArchiveChannelID"),
     (ulong)TL.Attribute("QuestionableArchiveChannelID"),
     (ulong)TL.Attribute("ExplicitArchiveChannelID"),
     (
      from U in TL.Element(xmlns+"RegisteredUsers").Elements(xmlns+"TaglistUser")
      select new TaglistRegisteredUser(
       (string)U.Attribute("Username"),
       ((bool)U.Attribute("InterestedInSafe") ? TaglistRegisteredUser.RatingFlags.Safe : TaglistRegisteredUser.RatingFlags.None)
       |((bool)U.Attribute("InterestedInQuestionable") ? TaglistRegisteredUser.RatingFlags.Questionable : TaglistRegisteredUser.RatingFlags.None)
       |((bool)U.Attribute("InterestedInExplicit") ? TaglistRegisteredUser.RatingFlags.Explicit : TaglistRegisteredUser.RatingFlags.None),
       (
        from C in ( U.Element(xmlns+"CategoryBlacklist")?.Elements(xmlns+"Category") ?? Enumerable.Empty<XElement>() )
        select (string)C.Attribute("Name")
       ).ToList()
      )
     ).ToImmutableHashSet()
    )
   ;
   Results=new Dictionary<string,Taglist>();
   foreach(Taglist Result in ResultsSource){
    try{
     Results.Add( Result.Name, Result );
    }catch(ArgumentException){
     throw new DataAccessException(string.Format("Duplicate Taglists with name '{0}'",Result.Name));
    }
   }
   return Results;
  }

  public async Task SaveAll(ICollection<Taglist> Save){
   Initialize();
   //TODO
   this.CachedTaglists=null;
  }

  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Taglists";
 }
}