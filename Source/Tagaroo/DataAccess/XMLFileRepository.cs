using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.IO;
using System.Reflection;

namespace Tagaroo.DataAccess{
 /// <summary>
 /// Common supporting code for Repositories that use XML files as their persistent data store.
 /// Each instance manages a particular XML file,
 /// which must be an instance document of an XML Schema
 /// that is available as an assembly resource in the assembly of this class,
 /// which it will be validated against during reads and writes.
 /// </summary>
 internal class XMLFileRepository{
  private readonly string DataFilePath;
  private readonly string NewFilePath, OldFilePath;
  private readonly XmlSchemaSet Schema=new XmlSchemaSet();
  private readonly XNamespace xmlns;
  private readonly string ResourceStreamName;
  
  /// <param name="DataFilePath">The path to the XML data file that persists the data</param>
  /// <param name="xmlns">The XML namespace of the XML Schema that the data file is an instance of, as well as the namespace of the root element of the instance document</param>
  /// <param name="ResourceStreamName">The identifying name of the assembly resource in this class' assembly that is the XML Schema associated with the instance document that this object will manage</param>
  public XMLFileRepository(string DataFilePath,XNamespace xmlns,string ResourceStreamName){
   this.DataFilePath=DataFilePath;
   this.xmlns=xmlns;
   this.ResourceStreamName=ResourceStreamName;
   this.NewFilePath = DataFilePath + SuffixNewFile;
   this.OldFilePath = DataFilePath + SuffixOldFile;
  }

  /// <summary>
  /// Should ideally be called once at some appropriate point after construction to perform some initialization,
  /// but will automatically be called at relevant points if not already called.
  /// </summary>
  public void Initialize(){
   if(Schema.Count>0){return;}
   this.Schema.Add(xmlns.NamespaceName,XmlReader.Create(
    Assembly.GetExecutingAssembly().GetManifestResourceStream(this.ResourceStreamName)
   ));
  }

  /// <summary>
  /// Opens the data file associated with this instance
  /// with the supplied <see cref="FileMode"/>, <see cref="FileAccess"/>, and <see cref="FileShare"/> values,
  /// returning a stream handle to the opened file.
  /// </summary>
  /// <exception cref="DataAccessException">Upon any problems opening the file</exception>
  public FileStream OpenFile(FileMode Mode,FileAccess Access,FileShare SharedAccess){
   //Initialize();
   return OpenFile(DataFilePath,Mode,Access,SharedAccess);
  }
  protected FileStream OpenFile(string Path,FileMode Mode,FileAccess Access,FileShare SharedAccess){
   try{
    return new FileStream(Path, Mode, Access, SharedAccess);
   }catch(FileNotFoundException Error){
    throw new DataAccessException(string.Format("The file '{0}' was not found; current working directory is {1}",Path,Environment.CurrentDirectory),Error);
   }catch(DirectoryNotFoundException Error){
    throw new DataAccessException(string.Format("One of the directories on the file's path '{0}' could not be opened",Path),Error);
   }catch(PathTooLongException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is too long for the current host platform",Path),Error);
   }catch(IOException Error){
    throw new DataAccessException(string.Format("IO error opening file: {0}",Error.Message),Error);
   }catch(UnauthorizedAccessException Error){
    throw new DataAccessException(string.Format("Access denied to file at '{0}'",Path),Error);
   }catch(System.Security.SecurityException Error){
    throw new DataAccessException(string.Format("Access denied to file at '{0}'",Path),Error);
   }catch(ArgumentException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is not a valid file path",Path),Error);
   }catch(NotSupportedException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is not a valid file path",Path),Error);
   }
  }

  /// <summary>
  /// Loads the data contained in <paramref name="DataFile"/> into an XML document,
  /// returning the loaded document as an <see cref="XDocument"/>.
  /// The data loaded will be validated according to the associated XML schema.
  /// </summary>
  /// <param name="DataFile">
  /// The stream containing the XML data to load, typically from <see cref="OpenFile"/>;
  /// the stream should be positioned at the start of the data to load
  /// </param>
  /// <param name="IgnoreWhitespace">If true, insignificant whitespace will be ignored and not included in the returned representation of the XML document</param>
  /// <exception cref="DataAccessException">
  /// If there is any problem loading data,
  /// such as the parsed XML document not being valid according to the associated schema,
  /// an error while parsing data read from the stream as XML,
  /// or an error reading data from the stream
  /// </exception>
  public async Task<XDocument> Load(Stream DataFile,bool IgnoreWhitespace=false){
   Initialize();
   XmlReaderSettings FileReaderSettings=new XmlReaderSettings(){
    Schemas = this.Schema,
    ValidationType = ValidationType.Schema,
    ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints,
    Async = true,
    IgnoreWhitespace = IgnoreWhitespace,
    CloseInput = false,
    XmlResolver = null
   };
   XmlReader FileReader=XmlReader.Create(DataFile, FileReaderSettings);
   XDocument Result;
   try{
    Result = await XDocument.LoadAsync(FileReader, LoadOptions.None, CancellationToken.None);
   }catch(XmlException Error){
    throw new DataAccessException(string.Format("XML data file parse error: {0}",Error.Message),Error);
   }catch(XmlSchemaValidationException Error){
    throw new DataAccessException(string.Format("The XML data file is not valid with respect to its schema: {0}",Error.Message),Error);
   }catch(IOException Error){
    throw new DataAccessException(string.Format("IO Error while reading data file: {0}",Error.Message),Error);
   }
   if( ! Result.Root.Name.Namespace.Equals(xmlns)){
    throw new DataAccessException(string.Format("The XML data file is not valid with respect to its schema: The namespace of the root element, '{1}', does not match the expected namespace '{0}'",xmlns,Result.Root.Name.Namespace));
   }
   return Result;
  }

  /// <summary>
  /// <see cref="OpenFile"/> with <see cref="FileMode.Open"/>, followed by <see cref="Load"/> if successful.
  /// Any stream returned by <see cref="OpenFile"/> will always be closed before the method finishes.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  public async Task<XDocument> LoadFile(FileAccess Access,FileShare SharedAccess,bool IgnoreWhitespace=false){
   Stream DataFile = OpenFile(FileMode.Open, Access, SharedAccess);
   try{
    return await Load(DataFile,IgnoreWhitespace);
   }finally{
    DataFile.Close();
   }
  }

  /// <summary>
  /// As for <see cref="LoadFile"/>, but the file is kept open,
  /// and a <see cref="Lock"/> holding onto the opened file is also returned.
  /// The nature of the lock will depend on the value of <paramref name="SharedAccess"/>.
  /// </summary>
  /// <exception cref="DataAccessException"/>
  public async Task<Tuple<XDocument,Lock>> LoadFileAndLock(FileAccess Access,FileShare SharedAccess,bool IgnoreWhitespace=false){
   Stream DataFile = OpenFile(FileMode.Open, Access, SharedAccess);
   XDocument Result=null;
   try{
    Result=await Load(DataFile,IgnoreWhitespace);
   }finally{
    if(Result is null){
     DataFile.Close();
    }
   }
   return new Tuple<XDocument,Lock>(
    Result,
    new FileLock(DataFile)
   );
  }

  /// <summary>
  /// Saves the XML document represented by <paramref name="ToSave"/> to the data file associated with this instance,
  /// overwriting the data file.
  /// The XML document is first validated according to the associated schema.
  /// The data file is overwritten in a safe manner, to protect against data loss.
  /// The XML document is initially written to a file of the same name as the data file but with the suffix <see cref="SuffixNewFile"/>.
  /// Any existing file with this name is overwritten.
  /// If this is successful, the existing data file is renamed to have a suffix of <see cref="SuffixOldFile"/>,
  /// overwriting any existing file with this name,
  /// and the newly written file has its <see cref="SuffixNewFile"/> stripped to become the new data file.
  /// </summary>
  /// <exception cref="DataAccessException">
  /// If there is any problem saving data,
  /// such as the XML document to save not being valid according to the associated schema,
  /// an error writing data to the new data file,
  /// or an error renaming any files
  /// </exception>
  public Task Save(XDocument ToSave, SaveOptions SavingOptions){
   return Save(ToSave,SavingOptions,null);
  }

  /// <summary>
  /// <para>
  /// Preconditions: <paramref name="Lock"/> must have been returned by a call to <see cref="LoadFileAndLock"/>
  /// </para>
  /// As for <see cref="Save(XDocument,SaveOptions)"/>;
  /// <paramref name="Lock"/> is released after this call, regardless of the call's success or failure.
  /// </summary>
  /// <exception cref="DataAccessException"><paramref name="Lock"/> will still be released even if this is thrown</exception>
  public async Task Save(XDocument ToSave, SaveOptions SavingOptions, Lock Lock){
   FileLock DataFileLock=null;
   if(!(Lock is null)){
    DataFileLock = Lock as FileLock;
    if(DataFileLock is null){throw new ArgumentException("The supplied Lock was not returned by this class");}
   }
   try{
    try{
     ToSave.Validate(this.Schema,null);
    }catch(XmlSchemaValidationException Error){
     throw new DataAccessException(string.Format("Schema validation error when converting to XML: {0}",Error.Message),Error);
    }
    FileStream NewFile = OpenFile(NewFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
    try{
     try{
      NewFile.SetLength(0);
     }catch(IOException Error){
      throw new DataAccessException("IO error whilst writing new data: "+Error.Message,Error);
     }
     try{
      await ToSave.SaveAsync(NewFile, SavingOptions, CancellationToken.None);
     }catch(IOException Error){
      throw new DataAccessException("IO error whilst writing data: "+Error.Message,Error);
     }
    }finally{
     NewFile.Close();
    }
    DataFileLock?.Release();
    try{
     File.Replace(NewFilePath, DataFilePath, OldFilePath, true);
    }catch(DriveNotFoundException Error){
     throw new DataAccessException(string.Format("Could not find the drive/filesystem for the file '{0}': ",DataFilePath)+Error.Message,Error);
    }catch(FileNotFoundException Error){
     throw new DataAccessException("A file could not be found during the save operation: "+Error.Message,Error);
    }catch(PathTooLongException Error){
     throw new DataAccessException(string.Format("The file path '{0}' is too long for the current host platform",DataFilePath),Error);
    }catch(IOException Error){
     throw new DataAccessException("IO error whilst saving data file: "+Error.Message,Error);
    }catch(UnauthorizedAccessException Error){
     throw new DataAccessException(string.Format("The file '{0}' or one of its reserved file names may have been marked as read-only, or a directory may have been created with one of those names: ",DataFilePath)+Error.Message,Error);
    }
   }finally{
    DataFileLock?.Release();
   }
  }

  public const string SuffixNewFile=".new";
  public const string SuffixOldFile=".old";
 }
}