using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using ICSharpCode.SharpZipLib.Zip;

namespace Gilmond.ImageProcessing.Functions
{

    public static class Ingest
    {
        [FunctionName("Ingest")]
        public static async Task Run([BlobTrigger("input/{controlFileName}.json", Connection = AzureConstants.BlobStorageConnectionStringName)]CloudBlockBlob controlBlob,
            string controlFileName, IBinder binder, TraceWriter log)
        {

            // 1. Read the text of the control file
            var controlFileText = await controlBlob.DownloadTextAsync();
            var control = JsonConvert.DeserializeObject<ProcessingControl>(controlFileText);
            log.Info($"Read control file {controlFileName}.json for ZIP file {control.File} - contains {control.Instructions.Count} processing instructions");

            // 2. Form the path of the zip file and bind the blob
            var zipFilePath = $"input/{control.File}";
            var zipFileBlob = await binder.BindToBlockBlob(zipFilePath, AzureConstants.BlobStorageConnectionStringName);

            // 3. Read the zip file stream
            var blobStream = new MemoryStream();
            await zipFileBlob.DownloadToStreamAsync(blobStream);
            blobStream.Position = 0;

            // 4. Decompress files out of zip file
            using (var zipStream = new ZipInputStream(blobStream))
            {
                ZipEntry currentEntry;
                while ((currentEntry = zipStream.GetNextEntry()) != null)
                {
                    var imageFilename = Path.GetFileName(currentEntry.Name);

                    log.Info($"Processing file {imageFilename} from {zipFilePath}");

                    // 5. Foreach file - form the output file path
                    var processImagePath = $"working/{controlFileName}/{imageFilename}";
                    var processControlFilePath = $"working/{controlFileName}/{imageFilename}.json";

                    // 6. Produce the control file for the image file
                    var processImageControl = new ProcessingControl()
                    {
                        File = imageFilename,
                        Instructions = control.Instructions
                    };
                    var processImageControlJson = JsonConvert.SerializeObject(processImageControl);

                    // 7. Write both to blobs - image file first - otherwise the next function could trigger before the image file is written.
                    var processImageBlob = await binder.BindToBlockBlob(processImagePath, AzureConstants.BlobStorageConnectionStringName);
                    await processImageBlob.UploadFromStreamAsync(zipStream);
                    log.Info($"Written image file to {processImagePath}");

                    var processImageControlBlob = await binder.BindToBlockBlob(processControlFilePath,
                        AzureConstants.BlobStorageConnectionStringName);
                    await processImageControlBlob.UploadTextAsync(processImageControlJson);
                    log.Info($"Written control blob to {processControlFilePath}");
                }
            }
            // 8. Remove the control file from input
            await controlBlob.DeleteIfExistsAsync();
        }
    }
}
