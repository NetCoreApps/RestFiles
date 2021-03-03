using System;
using System.IO;
using System.Linq;
using System.Net;
using RestFiles.ServiceModel;
using RestFiles.ServiceModel.Types;
using ServiceStack;
using ServiceStack.IO;

namespace RestFiles.ServiceInterface
{
    /// <summary>
    /// Define your ServiceStack web service request (i.e. Request DTO).
    /// </summary>
    public class FilesService : Service
    {
        public AppConfig Config { get; set; }

        public object Get(Files request)
        {
            var targetPath = GetAndValidateExistingPath(request);

            var isDirectory = VirtualFiles.IsDirectory(targetPath);

            if (!isDirectory && request.ForDownload)
                return new HttpResult(VirtualFiles.GetFile(targetPath), asAttachment: true);

            var response = isDirectory
                ? new FilesResponse { Directory = GetFolderResult(targetPath) }
                : new FilesResponse { File = GetFileResult(targetPath) };

            return response;
        }

        public object Post(Files request)
        {
            var targetDir = GetPath(request);

            if (VirtualFiles.IsFile(targetDir))
                throw new NotSupportedException(
                "POST only supports uploading new files. Use PUT to replace contents of an existing file");

            if (!VirtualFiles.DirectoryExists(targetDir))
            {
                VirtualFiles.WriteFile(targetDir.CombineWith(".temp"), "");
                VirtualFiles.DeleteFile(targetDir.CombineWith(".temp"));
            }
            
            foreach (var uploadedFile in base.Request.Files)
            {
                var newFilePath = targetDir.CombineWith(uploadedFile.FileName);
                VirtualFiles.WriteFile(newFilePath, uploadedFile.InputStream);
            }

            return new FilesResponse();
        }

        public void Put(Files request)
        {
            var targetFile = VirtualFiles.GetFile(GetAndValidateExistingPath(request));

            if (!Config.TextFileExtensions.Contains(targetFile.Extension))
                throw new NotSupportedException("PUT Can only update text files, not: " + targetFile.Extension);

            if (request.TextContents == null)
                throw new ArgumentNullException("TextContents");

            VirtualFiles.WriteFile(targetFile.VirtualPath, request.TextContents);
        }

        public void Delete(Files request)
        {
            var targetFile = GetAndValidateExistingPath(request);
            VirtualFiles.DeleteFile(targetFile);
        }

        private FolderResult GetFolderResult(string targetPath)
        {
            var result = new FolderResult();

            var dir = VirtualFiles.GetDirectory(targetPath);
            var subFolders = dir.Directories.ToList();
            foreach (var subDir in subFolders)
            {
                if (Config.ExcludeDirectories.Contains(subDir.Name)) continue;

                result.Folders.Add(new Folder
                {
                    Name = subDir.Name,
                    ModifiedDate = subDir.LastModified,
                    FileCount = subDir.GetFiles().Count(),
                });
            }

            foreach (var fileInfo in dir.GetFiles())
            {
                result.Files.Add(new ServiceModel.Types.File
                {
                    Name = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    FileSizeBytes = fileInfo.Length,
                    ModifiedDate = fileInfo.LastModified,
                    IsTextFile = Config.TextFileExtensions.Contains(fileInfo.Extension),
                });
            }

            return result;
        }

        private string GetPath(Files request)
        {
            return Config.RootDirectory.CombineWith(GetSafePath(request.Path));
        }

        private string GetAndValidateExistingPath(Files request)
        {
            var targetPath = GetPath(request);
            if (!VirtualFiles.IsFile(targetPath) && !VirtualFiles.IsDirectory(targetPath))
                throw new HttpError(HttpStatusCode.NotFound, new FileNotFoundException("Could not find: " + request.Path));

            return targetPath;
        }

        private FileResult GetFileResult(string filePath)
        {
            var file = VirtualFiles.GetFile(filePath);
            var isTextFile = Config.TextFileExtensions.Contains(file.Extension);

            return new FileResult
            {
                Name = file.Name,
                Extension = file.Extension,
                FileSizeBytes = file.Length,
                IsTextFile = isTextFile,
                Contents = isTextFile ? VirtualFiles.GetFile(file.VirtualPath).ReadAllText() : null,
                ModifiedDate = file.LastModified,
            };
        }

        public static string GetSafePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;

            //Strip invalid chars
            foreach (var invalidChar in Path.GetInvalidPathChars())
            {
                filePath = filePath.Replace(invalidChar.ToString(), string.Empty);
            }

            return filePath
                .TrimStart('.', '/', '\\')                  //Remove illegal chars at the start
                .Replace('\\', '/')                         //Switch all to use the same seperator
                .Replace("../", string.Empty)               //Remove access to top-level directories anywhere else 
                .Replace('/', Path.DirectorySeparatorChar); //Switch all to use the OS seperator
        }
    }
}