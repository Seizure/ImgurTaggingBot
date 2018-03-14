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
 
 /// <summary>
 /// Repository/DAO for Model-layer <see cref="Taglist"/> objects.
 /// A <see cref="DataAccessException"/> exception is thrown by many of the methods if there is some problem accessing the underlying data store.
 /// </summary>
 public interface TaglistRepository{
  /// <summary>
  /// Should ideally be called once at some appropriate point after the Repository has been constructed,
  /// allowing realizations to perform nontrivial initialization.
  /// However, realizations should not rely on this method being called before any other methods.
  /// </summary>
  void Initialize();

  /// <summary>
  /// Retrieves a collection of all Taglists, keyed by the identifying <see cref="Taglist.Name"/>.
  /// This method does not explicitly populate the <see cref="Taglist.RegisteredUsers"/> property,
  /// retrieving only the <see cref="Taglist"/> objects themselves and none of their component objects.
  /// As such, calling code should not access the <see cref="Taglist.RegisteredUsers"/> property in any of the results.
  /// The returned collection is immutable, to allow efficient implementation of cacheing Decorators.
  /// To retrieve the full details of a particular <see cref="Taglist"/>, use <see cref="Load"/>.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task<IReadOnlyDictionary<string,Taglist>> ReadAllHeaders();

  /// <summary>
  /// Retrieves a collection of all Taglists, keyed by the identifying <see cref="Taglist.Name"/>;
  /// the <see cref="ReadAllHeaders"/> and <see cref="Load"/> methods should be used in preference to this method.
  /// The returned collection is immutable, to allow efficient implementation of cacheing Decorators.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  Task<IReadOnlyDictionary<string,Taglist>> LoadAll();

  /// <summary>
  /// Read method for Model-layer <see cref="Taglist"/> objects, retrieving one by its identifying <see cref="Taglist.Name"/>.
  /// </summary>
  /// <exception cref="EntityNotFoundException">No Taglist with the specified Name exists in the data store</exception>
  /// <exception cref="DataAccessException"/>
  Task<Taglist> Load(string TaglistName);

  /// <summary>
  /// As for <see cref="Load"/>, but also returns a <see cref="Lock"/> for the returned <see cref="Taglist"/>
  /// that prevents changes to it from elsewhere from being saved.
  /// Calling code should release the <see cref="Lock"/> as soon as possible,
  /// either by calling <see cref="Save"/> or <see cref="Lock.Release"/> directly.
  /// </summary>
  /// <exception cref="EntityNotFoundException">No Taglist with the specified Name exists in the data store</exception>
  /// <exception cref="DataAccessException"/>
  Task<Tuple<Taglist,Lock>> LoadAndLock(string TaglistName);

  /// <summary>
  /// <para>
  /// Preconditions: <paramref name="Lock"/> must have been returned by a call to <see cref="LoadAndLock"/>,
  /// be associated with <paramref name="ToSave"/>,
  /// and <see cref="Lock.Release"/> must not have been called on it
  /// </para>
  /// Update method for Model-layer <see cref="Taglist"/> objects.
  /// The <see cref="Taglist"/> to update must have been retrieved by a call to <see cref="LoadAndLock"/>,
  /// and it's associated <see cref="Lock"/> must be supplied.
  /// <paramref name="Lock"/> is released after this call, regardless of the call's success or failure.
  /// </summary>
  /// <exception cref="DataAccessException"><paramref name="Lock"/> will still be released even if this is thrown</exception>
  Task Save(Taglist ToSave,Lock Lock);
 }
 
 /// <summary>
 /// Main Realization of <see cref="TaglistRepository"/>,
 /// implementing a Repository that uses an XML file as the persistent data store.
 /// XML instance documents must be valid with respect to the XML Schema in the file Taglists.xsd.
 /// The <see cref="Lock"/> objects returned by <see cref="LoadAndLock"/> actually lock all Taglists,
 /// as they are implemented as file locks on the data store file.
 /// </summary>
 public class TaglistRepositoryMain : TaglistRepository{
  private readonly XMLFileRepository XMLDataFileHandler;
  private readonly string DataFilePath;
  
  /// <param name="DataFilePath">The file path to the XML instance document serving as the data store</param>
  public TaglistRepositoryMain(string DataFilePath){
   this.XMLDataFileHandler = new XMLFileRepository(DataFilePath,xmlns,"Tagaroo.DataAccess.Taglists.xsd");
   this.DataFilePath=DataFilePath;
  }

  public void Initialize(){
   XMLDataFileHandler.Initialize();
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

  public async Task<IReadOnlyDictionary<string,Taglist>> ReadAllHeaders(){
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
   return TaglistsDictionary(Results);
  }

  public async Task<IReadOnlyDictionary<string,Taglist>> LoadAll(){
   XDocument DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.Read, FileShare.Read, true);
   IEnumerable<Taglist> ResultsSource=
    from TL in DataDocument.Root.Elements(xmlns+"Taglist")
    select FromTaglistXML(TL)
   ;
   return TaglistsDictionary(ResultsSource);
  }

  private IReadOnlyDictionary<string,Taglist> TaglistsDictionary(IEnumerable<Taglist> ResultsSource){
   Dictionary<string,Taglist> Results=new Dictionary<string,Taglist>();
   foreach(Taglist Result in ResultsSource){
    try{
     Results.Add( Result.Name, Result );
    }catch(ArgumentException){
     throw new DataAccessException(string.Format("Duplicate Taglists with name '{0}'",Result.Name));
    }
   }
   return Results;
  }

  public async Task<Taglist> Load(string TaglistName){
   return (await Load(TaglistName,false)).Item1;
  }
  
  public Task<Tuple<Taglist,Lock>> LoadAndLock(string TaglistName){
   return Load(TaglistName,true);
  }

  protected async Task<Tuple<Taglist,Lock>> Load(string TaglistName,bool LockDataFile){
   TaglistName=TaglistName.Normalize(NormalizationForm.FormKD);
   XDocument DataDocument;
   Lock DataFileLock=null;
   if(LockDataFile){
    var LoadResult = await XMLDataFileHandler.LoadFileAndLock(FileAccess.ReadWrite, FileShare.None, true);
    DataDocument = LoadResult.Item1;
    DataFileLock = LoadResult.Item2;
   }else{
    DataDocument = await XMLDataFileHandler.LoadFile(FileAccess.ReadWrite, FileShare.None, true);
   }
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
     DataFileLock?.Release();
    }
   }
  }

  public Task Save(Taglist ToSave,Lock Lock){
   Initialize();
   TaglistRepositoryLock DataFileLock=Lock as TaglistRepositoryLock;
   if(DataFileLock is null){throw new ArgumentException("The supplied Lock was not returned by this class");}
   if(DataFileLock.Released){throw new ArgumentException("The supplied Lock has been released");}
   try{
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
   }finally{
    DataFileLock.Release();
   }
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
  /// <summary>
  /// The expected XML namespace of the root element of instance documents, as defined in Taglists.xsd.
  /// </summary>
  static protected readonly XNamespace xmlns="urn:xmlns:tagaroo:Taglists:v1.0";
 }

 internal class TaglistRepositoryLock : Lock{
  public readonly Lock Decorated;
  public readonly XDocument LoadedDataDocument;
  public bool Released{get;private set;}
  public TaglistRepositoryLock(Lock Decorated,XDocument LoadedDataDocument){
   this.Decorated=Decorated;
   this.LoadedDataDocument=LoadedDataDocument;
   this.Released=false;
  }
  public void Release(){
   this.Released=true;
   Decorated.Release();
  }
  void IDisposable.Dispose(){
   Release();
  }
 }
}