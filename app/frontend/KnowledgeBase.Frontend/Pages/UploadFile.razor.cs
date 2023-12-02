using System.Text;
using Dapr.Client;
using KnowledgeBase.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.JSInterop;

namespace KnowledgeBase.Frontend;

public class UploadFileBase : ComponentBase
{
    [Inject] private IJSRuntime JSRuntime { get; set; }
    [Inject] private DaprClient daprClient { get; set; }

    protected ElementReference dropZoneElement;
    protected InputFile inputFile;

    protected IJSObjectReference _module;
    protected IJSObjectReference _dropZoneInstance;

    protected int uploadResult { get; set; } = -1;
    protected bool isVisible { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Load the JS file
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./dropZone.js");

            // Initialize the drop zone
            _dropZoneInstance = await _module.InvokeAsync<IJSObjectReference>("initializeFileDropZone", dropZoneElement, inputFile.Element);
        }
    }

    protected async Task LoadFiles(InputFileChangeEventArgs e)
    {
        ShowSpinner();

        try
        {
            var storageAccountEndpoint = (await daprClient.GetSecretAsync("skragdemoakv", "AzureStorageAccountEndpoint")).Values.FirstOrDefault();
            var storageAccountContainer = (await daprClient.GetSecretAsync("skragdemoakv", "AzureStorageContainer")).Values.FirstOrDefault();
            foreach (var file in e.GetMultipleFiles())
            {
                await UploadFileAsync(file);
                await daprClient.PublishEventAsync("skragdemoqueue", "documentprocess", new DocumentProcessing { BlobName = file.Name, BlobUri = $"{storageAccountEndpoint}/{storageAccountContainer}/{file.Name}" });
            }
            uploadResult = 1;
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
            uploadResult = 0;
        }

        HideSpinner();

        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dropZoneInstance != null)
        {
            await _dropZoneInstance.InvokeVoidAsync("dispose");
            await _dropZoneInstance.DisposeAsync();
        }

        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }

    private async Task UploadFileAsync(IBrowserFile file)
    {
        Console.WriteLine($"Prepraring to upload file {file.Name}...");
        var storageAccountEndpoint = (await daprClient.GetSecretAsync("skragdemoakv", "AzureStorageAccount")).Values.FirstOrDefault();
        var storageKey = (await daprClient.GetSecretAsync("skragdemoakv", "AzureStorageAccountKey")).Values.FirstOrDefault();
        var storageCredentials = new StorageCredentials(storageAccountEndpoint, storageKey);
        
        var defaultContainerName = (await daprClient.GetSecretAsync("skragdemoakv", "AzureStorageContainer")).Values.FirstOrDefault();

        var storageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);
        var blobClient = storageAccount.CreateCloudBlobClient();
        
        CloudBlobContainer container = blobClient.GetContainerReference(defaultContainerName);

        CloudBlockBlob blockBlob = container.GetBlockBlobReference(file.Name);

        var blockIds = new List<string>();
        var blockNumber = 0;
        var bufferSize = 4 * 1024 * 1024;
        var buffer = new byte[bufferSize]; // 4 MB buffer
        int bytesRead;

        using (var stream = file.OpenReadStream(15 * 1024 * 1024))
        {
            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead > 0)
                {
                    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(blockNumber.ToString("d6")));
                    await blockBlob.PutBlockAsync(blockId, new MemoryStream(buffer, 0, bytesRead), null);
                    blockIds.Add(blockId);
                    blockNumber++;
                }
            } while (bytesRead > 0);
        }
        await blockBlob.PutBlockListAsync(blockIds);

        // using (var fileStream = file.OpenReadStream())
        // {
        //     Console.WriteLine($"Uploading file {file.Name}...");
        //     blockBlob.Properties.ContentType = file.ContentType;
        //     await blockBlob.UploadFromStreamAsync(fileStream);
        // }
    }

    protected void ShowSpinner()
    {
        isVisible = true;
        StateHasChanged();
    }

    protected void HideSpinner()
    {
        isVisible = false;
        StateHasChanged();
    }
}