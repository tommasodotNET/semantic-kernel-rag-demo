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

public class UploadFileBase : ComponentBase, IAsyncDisposable
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
            var storageAccountEndpoint = (await daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageAccountEndpoint")).Values.FirstOrDefault();
            foreach (var file in e.GetMultipleFiles())
            {
                await UploadFileAsync(file);
                var fileName = Path.GetFileNameWithoutExtension(file.Name);
                await daprClient.PublishEventAsync("skcodemotion2023queue", "knowledgeprocess", new DocumentProcessing { BlobName = fileName, BlobUri = $"{storageAccountEndpoint}/{fileName}" });
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error uploading file: {ex.Message}");
            uploadResult = 0;
        }

        HideSpinner();

        uploadResult = 1;
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
        var storageAccountEnpoint = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageAccountEndpoint").GetAwaiter().GetResult().Values.FirstOrDefault();
        var storageKey = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageAccountKey").GetAwaiter().GetResult().Values.FirstOrDefault();
        var storageCredentials = new StorageCredentials(storageAccountEnpoint, storageKey);
        
        var defaultContainerName = daprClient.GetSecretAsync("skcodemotion2023akv", "AzureStorageContainer").GetAwaiter().GetResult().Values.FirstOrDefault();

        var storageAccount = new CloudStorageAccount(storageCredentials, useHttps: true);
        var blobClient = storageAccount.CreateCloudBlobClient();

        var blockIds = new List<string>();
        var blockNumber = 0;
        var bufferSize = 4 * 1024 * 1024;
        var buffer = new byte[bufferSize]; // 4 MB buffer
        int bytesRead;

        var container = blobClient.GetContainerReference(defaultContainerName);
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