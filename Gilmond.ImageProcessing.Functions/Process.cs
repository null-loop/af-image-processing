using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Gilmond.ImageProcessing.Functions
{
    public static class Process
    {
        [FunctionName("Process")]
        public static async Task Run([BlobTrigger("working/{set}/{controlFileName}.json", Connection = AzureConstants.BlobStorageConnectionStringName)]CloudBlockBlob controlBlob,
            string set, string controlFileName, IBinder binder, TraceWriter log)
        {
            // 1. Read the text of the control file
            var controlFileText = await controlBlob.DownloadTextAsync();
            var control = JsonConvert.DeserializeObject<ProcessingControl>(controlFileText);
            log.Info($"Read control file {controlFileName}.json for image file {set}/{control.File} - contains {control.Instructions.Count} processing instructions");

            // 2. Form the path to the image file
            var imageFilePath = $"working/{set}/{control.File}";
            var imageFileBlob = await binder.BindToBlockBlob(imageFilePath, AzureConstants.BlobStorageConnectionStringName);

            // 3. Read the bytes of the image file
            var blobStream = new MemoryStream();
            await imageFileBlob.DownloadToStreamAsync(blobStream);
            blobStream.Position = 0;

            // 4. Check if there are more instructions
            if (control.Instructions.Count > 0)
            {
                // 4a. Dequeue and perform the next processing instruction
                var nextInstruction = control.Instructions.Dequeue();
                var nextImageBytes = ImageProcessor.ProcessInstruction(nextInstruction, blobStream);

                if (nextInstruction.Operation != ProcessingOperation.ChangeFormat)
                {
                    // 4b. Write the bytes of the image
                    await imageFileBlob.UploadFromByteArrayAsync(nextImageBytes, 0, nextImageBytes.Length);
                    // 4c. Write the text of the control file
                    var newControlFileJson = JsonConvert.SerializeObject(control);
                    await controlBlob.UploadTextAsync(newControlFileJson);
                }
                else
                {
                    // we've changed the format - so the filename needs to change
                    var shortNewImageFilename = $"{Path.GetFileNameWithoutExtension(imageFilePath)}.{nextInstruction.Arguments[0]}";
                    var newImageFilename = $"working/{set}/{shortNewImageFilename}";
                    // change the name in the control file
                    control.File = shortNewImageFilename;
                    // delete the original file
                    await imageFileBlob.DeleteIfExistsAsync();
                    // get the new blob
                    var newImageFileBlob = await binder.BindToBlockBlob(newImageFilename, AzureConstants.BlobStorageConnectionStringName);
                    // write the new image bytes
                    await newImageFileBlob.UploadFromByteArrayAsync(nextImageBytes, 0, nextImageBytes.Length);
                    // write the updated control file
                    var newControlFileJson = JsonConvert.SerializeObject(control);
                    await controlBlob.UploadTextAsync(newControlFileJson);
                }
            }
            else
            {
                // 5. Form the path of the output file
                var outputFilePath = $"output/{set}/{control.File}";
                log.Info($"No processing to perform - writing to output file {outputFilePath}");

                var outputFile = await binder.BindToBlockBlob(outputFilePath, AzureConstants.BlobStorageConnectionStringName);
                // 6. Write the final output image file
                await outputFile.UploadFromStreamAsync(blobStream);
                // 7. Remove the working image file
                await imageFileBlob.DeleteIfExistsAsync();
                // 8. Remove the control file
                await controlBlob.DeleteIfExistsAsync();
            }

        }
    }
}
