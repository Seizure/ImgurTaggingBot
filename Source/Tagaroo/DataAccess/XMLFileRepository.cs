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
 internal class XMLFileRepository{
  private readonly string DataFilePath;
  private readonly string NewFilePath, OldFilePath;
  private readonly XmlSchemaSet Schema=new XmlSchemaSet();
  private readonly XNamespace xmlns;
  private readonly string ResourceStreamName;
  
  public XMLFileRepository(string DataFilePath,XNamespace xmlns,string ResourceStreamName){
   this.DataFilePath=DataFilePath;
   this.xmlns=xmlns;
   this.ResourceStreamName=ResourceStreamName;
   this.NewFilePath = DataFilePath + SuffixNewFile;
   this.OldFilePath = DataFilePath + SuffixOldFile;
  }

  public void Initialize(){
   if(Schema.Count>0){return;}
   this.Schema.Add(xmlns.NamespaceName,XmlReader.Create(
    Assembly.GetExecutingAssembly().GetManifestResourceStream(this.ResourceStreamName)
   ));
  }

  /// <exception cref="DataAccessException"/>
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

  /// <exception cref="DataAccessException"/>
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
   try{
    return await XDocument.LoadAsync(FileReader, LoadOptions.None, CancellationToken.None);
   }catch(XmlException Error){
    throw new DataAccessException(string.Format("XML data file parse error: {0}",Error.Message),Error);
   }catch(XmlSchemaValidationException Error){
    throw new DataAccessException(string.Format("The XML data file is not valid with respect to its schema: {0}",Error.Message),Error);
   }catch(IOException Error){
    throw new DataAccessException(string.Format("IO Error while reading data file: {0}",Error.Message),Error);
   }
  }

  /// <exception cref="DataAccessException"/>
  public async Task<XDocument> LoadFile(FileAccess Access,FileShare SharedAccess,bool IgnoreWhitespace=false){
   Stream DataFile = OpenFile(FileMode.Open, Access, SharedAccess);
   try{
    return await Load(DataFile,IgnoreWhitespace);
   }finally{
    DataFile.Close();
   }
  }

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

  /// <exception cref="DataAccessException"/>
  public Task Save(XDocument ToSave, SaveOptions SavingOptions){
   return Save(ToSave,SavingOptions,null);
  }

  /// <exception cref="DataAccessException"/>
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

  private const string SuffixNewFile=".new";
  private const string SuffixOldFile=".old";
 }
}