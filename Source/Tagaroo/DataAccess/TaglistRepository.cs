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

  /*
  /// <exception cref="DataAccessException"/>
  Task Verify();
  */

  /// <exception cref="DataAccessException"/>
  Task<IReadOnlyCollection<Taglist>> ReadAllHeaders();

  /// <exception cref="DataAccessException"/>
  Task<IReadOnlyDictionary<string,Taglist>> LoadAll();

  /// <exception cref="EntityNotFoundException"/>
  /// <exception cref="DataAccessException"/>
  Task<Tuple<Taglist,Lock>> LoadAndLock(string TaglistName);

  /// <exception cref="DataAccessException"/>
  Task Save(Taglist ToSave,Lock Lock);
 }
 
 public class TaglistRepositoryMain : TaglistRepository{
  private readonly XMLFileRepository XMLDataFileHandler;
  private readonly string DataFilePath;
  
  public TaglistRepositoryMain(string DataFilePath){
   this.XMLDataFileHandler = new XMLFileRepository(DataFilePath,xmlns,"Tagaroo.DataAccess.Taglists.xsd");
   this.DataFilePath=DataFilePath;
  }

  public void Initialize(){
   XMLDataFileHandler.Initialize();
  }
  
  /*
  public async Task Verify(){
   try{
    await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read);
   }catch(DataAccessException){
    throw;
   }
  }
  */

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

  public async Task<IReadOnlyCollection<Taglist>> ReadAllHeaders(){
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read, true);
   IEnumerable<Taglist> Results=
    from TL in DataDocument.Root.Elements(xmlns+"Taglist")
    select new Taglist(
     (string)TL.Attribute("Name"),
     (ulong)TL.Attribute("SafeArchiveChannelID"),
     (ulong)TL.Attribute("QuestionableArchiveChannelID"),
     (ulong)TL.Attribute("ExplicitArchiveChannelID"),
     ImmutableHashSet<TaglistRegisteredUser>.Empty
    )
   ;
   return Results.ToList();
  }

  public async Task<IReadOnlyDictionary<string,Taglist>> LoadAll(){
   Dictionary<string,Taglist> Results;
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read, true);
   IEnumerable<Taglist> ResultsSource=
    from TL in DataDocument.Root.Elements(xmlns+"Taglist")
    select FromTaglistXML(TL)
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

  public async Task<Tuple<Taglist,Lock>> LoadAndLock(string TaglistName){
   TaglistName=TaglistName.Normalize(NormalizationForm.FormKD);
   var LoadResult = await XMLDataFileHandler.LoadFileAndLock(FileAccess.ReadWrite, FileShare.None, true);
   XDocument DataDocument = LoadResult.Item1;
   Lock DataFileLock = LoadResult.Item2;
   bool success=false;
   try{
    Taglist Result=(
     from TL in DataDocument.Root.Elements(xmlns+"Taglist")
     where TaglistName.Equals(
      ((string)TL.Attribute("Name")).Normalize(NormalizationForm.FormKD),
      StringComparison.Ordinal
     )
     select FromTaglistXML(TL)
    ).FirstOrDefault();
    if(Result is null){
     throw new EntityNotFoundException();
    }
    success=true;
    return new Tuple<Taglist,Lock>(Result,new TaglistRepositoryLock(DataFileLock,DataDocument));
   }finally{
    if(!success){
     DataFileLock.Release();
    }
   }
  }

  public Task Save(Taglist ToSave,Lock Lock){
   Initialize();
   TaglistRepositoryLock DataFileLock=Lock as TaglistRepositoryLock;
   if(DataFileLock is null){throw new ArgumentException("The supplied Lock was not returned by this class");}
   XDocument DataDocument = DataFileLock.LoadedDataDocument;
   XElement OldTaglistElement = (
    from TL in DataDocument.Root.Elements(xmlns+"Taglist")
    where ToSave.Name.Equals(
     ((string)TL.Attribute("Name")).Normalize(NormalizationForm.FormKD),
     StringComparison.Ordinal
    )
    select TL
   ).FirstOrDefault();
   if(!(OldTaglistElement is null)){
    OldTaglistElement.Remove();
   }
   XElement NewTaglistElement = ToTaglistXML(ToSave);
   DataDocument.Root.Add(NewTaglistElement);
   return XMLDataFileHandler.Save(DataDocument,SavingOptions,DataFileLock.Decorated);
  }

  protected Taglist FromTaglistXML(XElement TL){
   return new Taglist(
    (string)TL.Attribute("Name"),
    (ulong)TL.Attribute("SafeArchiveChannelID"),
    (ulong)TL.Attribute("QuestionableArchiveChannelID"),
    (ulong)TL.Attribute("ExplicitArchiveChannelID"),
    (
     from U in TL.Element(xmlns+"RegisteredUsers").Elements(xmlns+"TaglistUser")
     select new TaglistRegisteredUser(
      (string)U.Attribute("Username"),
      (int)U.Attribute("UserID"),
      ((bool)U.Attribute("InterestedInSafe") ? TaglistRegisteredUser.RatingFlags.Safe : TaglistRegisteredUser.RatingFlags.None)
      |((bool)U.Attribute("InterestedInQuestionable") ? TaglistRegisteredUser.RatingFlags.Questionable : TaglistRegisteredUser.RatingFlags.None)
      |((bool)U.Attribute("InterestedInExplicit") ? TaglistRegisteredUser.RatingFlags.Explicit : TaglistRegisteredUser.RatingFlags.None),
      (
       from C in ( U.Element(xmlns+"CategoryBlacklist")?.Elements(xmlns+"Category") ?? Enumerable.Empty<XElement>() )
       select (string)C.Attribute("Name")
      ).ToList()
     )
    ).ToImmutableHashSet()
   );
  }

  protected XElement ToTaglistXML(Taglist Model){
   return new XElement(xmlns+"Taglist",
    new XAttribute("Name",Model.Name),
    new XAttribute("SafeArchiveChannelID",Model.ArchiveChannelIDSafe),
    new XAttribute("QuestionableArchiveChannelID",Model.ArchiveChannelIDQuestionable),
    new XAttribute("ExplicitArchiveChannelID",Model.ArchiveChannelIDExplicit),
    new XElement(xmlns+"RegisteredUsers",(
     from U in Model.RegisteredUsers
     select new XElement(xmlns+"TaglistUser",
      new XAttribute("Username",U.Username),
      new XAttribute("UserID",U.ID),
      new XAttribute("InterestedInSafe",U.AcceptsRating(Ratings.Safe)),
      new XAttribute("InterestedInQuestionable",U.AcceptsRating(Ratings.Questionable)),
      new XAttribute("InterestedInExplicit",U.AcceptsRating(Ratings.Explicit)),
      new XElement(xmlns+"CategoryBlacklist",(
       from C in U.CategoryBlacklist
       select new XElement(xmlns+"Category",
        new XAttribute("Name",C)
       )
      ))
     )
    ))
   );
  }

  protected const SaveOptions SavingOptions=SaveOptions.OmitDuplicateNamespaces;
  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Taglists:v1-snapshot";
 }

 internal class TaglistRepositoryLock : Lock{
  public readonly Lock Decorated;
  public readonly XDocument LoadedDataDocument;
  public TaglistRepositoryLock(Lock Decorated,XDocument LoadedDataDocument){
   this.Decorated=Decorated;
   this.LoadedDataDocument=LoadedDataDocument;
  }
  public void Release(){
   Decorated.Release();
  }
  void IDisposable.Dispose(){
   Decorated.Dispose();
  }
 }
}