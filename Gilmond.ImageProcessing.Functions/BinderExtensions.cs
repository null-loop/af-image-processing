using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Gilmond.ImageProcessing.Functions
{
    public static class BinderExtensions
    {
        public static async Task<CloudBlockBlob> BindToBlockBlob(this IBinder binder, string blobPath, string blobConnectionStringName)
        {
            return await binder.BindAsync<CloudBlockBlob>(
                    new BlobAttribute(blobPath)
                    {
                        Connection = blobConnectionStringName
                    })
                .ConfigureAwait(false);
        }
        
    }

}
