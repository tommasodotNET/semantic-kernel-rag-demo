using System.Text;
using Dapr.Client;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;

namespace KnowledgeBase.Frontend.Services;

public interface IBlobStorageManager
{
    Task UploadFileAsync(IBrowserFile file, string containerName = "");

    Task<CloudBlockBlob> GetCloudBlockBlobAsync(string blobName, string containerName = "");

    Task UploadTextAsync(string blobName, string text, string containerName = "");

    Task<List<IListBlobItem>> GetListBlobAsync(string containerName = "");
}

public class BlobStorageManager : IBlobStorageManager
{
    private readonly CloudStorageAccount _storageAccount;
    private readonly CloudBlobClient _blobClient;
    private readonly string _defaultContainerName;

    public BlobStorageManager([FromServices]DaprClient daprClient)
    {
        var storageAccount = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageAccountEndpoint").GetAwaiter().GetResult().Values.FirstOrDefault();
        var storageKey = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageAccountKey").GetAwaiter().GetResult().Values.FirstOrDefault();
        var storageCredentials = new StorageCredentials(storageAccount, storageKey);
        
        _defaultContainerName = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageContainer").GetAwaiter().GetResult().Values.FirstOrDefault();

        _storageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);
        _blobClient = _storageAccount.CreateCloudBlobClient();
    }

    public async Task<List<IListBlobItem>> GetListBlobAsync(string containerName = "")
    {
        var container = _blobClient.GetContainerReference(containerName == "" ? _defaultContainerName : containerName);
        await container.CreateIfNotExistsAsync();

        var blobList = new List<IListBlobItem>();
        BlobContinuationToken continuationToken = null;
        do
        {
            var resultSegment = await container.ListBlobsSegmentedAsync(null, continuationToken);
            continuationToken = resultSegment.ContinuationToken;
            blobList.AddRange(resultSegment.Results);
        } while (continuationToken != null);

        return blobList;
    }

    public async Task UploadFileAsync(IBrowserFile file, string containerName = "")
    {
        var blockIds = new List<string>();
        var blockNumber = 0;
        var bufferSize = 4 * 1024 * 1024;
        var buffer = new byte[bufferSize]; // 4 MB buffer
        int bytesRead;

        var container = _blobClient.GetContainerReference(containerName == "" ? _defaultContainerName : containerName);
        await container.CreateIfNotExistsAsync();

        var blobFile = container.GetBlockBlobReference(Path.GetFileNameWithoutExtension(file.Name));
        using (var stream = file.OpenReadStream(15 * 1024 * 1024))
        {
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(blockNumber.ToString("d6")));
                    await blobFile.PutBlockAsync(blockId, new MemoryStream(buffer, 0, bytesRead), null);
                    blockIds.Add(blockId);
                    blockNumber++;
                }
            } while (bytesRead > 0);
        }
        await blobFile.PutBlockListAsync(blockIds);
    }

    public async Task UploadTextAsync(string blobName, string text, string containerName = "")
    {
        var container = _blobClient.GetContainerReference(containerName == "" ? _defaultContainerName : containerName);
        await container.CreateIfNotExistsAsync();

        var blobFile = container.GetBlockBlobReference(blobName);
        await blobFile.UploadTextAsync(text);
    }

    public async Task<CloudBlockBlob> GetCloudBlockBlobAsync(string blobName, string containerName = "")
    {
        var container = _blobClient.GetContainerReference(containerName == "" ? _defaultContainerName : containerName);
        await container.CreateIfNotExistsAsync();

        return container.GetBlockBlobReference(blobName);
    }
}