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
  private readonly XmlSchemaSet Schema=new XmlSchemaSet();
  private readonly XNamespace xmlns;
  private readonly string ResourceStreamName;
  
  public XMLFileRepository(string DataFilePath,XNamespace xmlns,string ResourceStreamName){
   this.DataFilePath=DataFilePath;
   this.xmlns=xmlns;
   this.ResourceStreamName=ResourceStreamName;
  }

  public void Initialize(){
   if(Schema.Count>0){return;}
   this.Schema.Add(xmlns.NamespaceName,XmlReader.Create(
    Assembly.GetExecutingAssembly().GetManifestResourceStream(this.ResourceStreamName)
   ));
  }

  /// <exception cref="DataAccessException"/>
  public Stream OpenFile(FileMode Mode,FileAccess Access,FileShare SharedAccess){
   //Initialize();
   try{
    return new FileStream(this.DataFilePath, Mode, Access, SharedAccess);
   }catch(FileNotFoundException Error){
    throw new DataAccessException(string.Format("The file '{0}' was not found; current working directory is {1}",DataFilePath,Environment.CurrentDirectory),Error);
   }catch(DirectoryNotFoundException Error){
    throw new DataAccessException(string.Format("One of the directories on the file's path '{0}' could not be opened",DataFilePath),Error);
   }catch(PathTooLongException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is too long for the current host platform",DataFilePath),Error);
   }catch(IOException Error){
    throw new DataAccessException(string.Format("IO error opening file: {0}",Error.Message),Error);
   }catch(UnauthorizedAccessException Error){
    throw new DataAccessException(string.Format("Access denied to file at '{0}'",DataFilePath),Error);
   }catch(System.Security.SecurityException Error){
    throw new DataAccessException(string.Format("Access denied to file at '{0}'",DataFilePath),Error);
   }catch(ArgumentException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is not a valid file path",DataFilePath),Error);
   }catch(NotSupportedException Error){
    throw new DataAccessException(string.Format("The file path '{0}' is not a valid file path",DataFilePath),Error);
   }
  }

  /// <exception cref="DataAccessException"/>
  public async Task<XDocument> Load(Stream DataFile){
   Initialize();
   XmlReaderSettings FileReaderSettings=new XmlReaderSettings(){
    Schemas = this.Schema,
    ValidationType = ValidationType.Schema,
    ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints,
    Async = true,
    IgnoreWhitespace = false,
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
  public async Task<XDocument> LoadFile(FileAccess Access,FileShare SharedAccess){
   Stream DataFile = OpenFile(FileMode.Open, Access, SharedAccess);
   try{
    return await Load(DataFile);
   }finally{
    DataFile.Close();
   }
  }
 }
}