using Dapr.Client;
using KnowledgeBase.Frontend.Services;
using KnowledgeBase.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Azure.Storage.Blob;
using Microsoft.JSInterop;

namespace KnowledgeBase.Frontend;

public class UploadFileBase : ComponentBase, IAsyncDisposable
{
    [Inject] private IBlobStorageManager BlobStorageManager { get; set; }
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
                await BlobStorageManager.UploadFileAsync(file);
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