// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using System.IO;
using Azure.Storage.Sas;
using System;
using Azure.Storage;
using Azure.Storage.Blobs.Models;
using AudioFilesRestApi.helper;

namespace AudioFilesRestApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AudioFilesUploadController : ControllerBase
    {
        // Max request body size limit for an upload controller action, in bytes
        private const int RequestSizeLimit = 40000000;
        
        private readonly ILogger<AudioFilesUploadController> _logger;

        private readonly IConfiguration _config;

        public AudioFilesUploadController(IConfiguration appConfig, ILogger<AudioFilesUploadController> logger)
        {
            _config = appConfig;
            _logger = logger;
        }

        [HttpPost]
        [Route("/api/[controller]", Name = "UploadFile")]
        [RequestSizeLimit(RequestSizeLimit)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadFileAsync(IFormFile file)
        {
            if (!Request.HasFormContentType)
            {
                return BadRequest("Unsupported media type");
            }

            if (string.IsNullOrWhiteSpace(file.FileName))
            {
                return BadRequest("An error has occured while uploading your file. Please try again.");
            }

            if (_config["StorageAccount:ConnectionString"] is var storageAccountConnectionString && string.IsNullOrWhiteSpace(storageAccountConnectionString))
            {
                return BadRequest("Invalid, empty or corrupted storage account connection string");
            };

            if (_config["StorageAccount:ContainerName"] is var storageAccountContainerName && string.IsNullOrWhiteSpace(storageAccountContainerName))
            {
                return BadRequest("Invalid, empty or corrupted storage account container name");
            };

            _logger.LogInformation($"StorageAccount:ConnectionString - {storageAccountConnectionString}");
            _logger.LogInformation($"StorageAccount:ContainerName - {storageAccountContainerName}");

            // Create a BlobServiceClient object which will be used to create a container client
            var blobServiceClient = new BlobServiceClient(storageAccountConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(storageAccountContainerName);
            await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);

            using (var stream = file.OpenReadStream())
            {
                await containerClient.UploadBlobAsync(Path.Combine(new DirectoryInfo(file.FileName).Parent.Name, Path.GetFileName(file.FileName)),
                                                                stream).ConfigureAwait(false);
            }

            return Ok($"File: {file.FileName} has successfully uploaded");
        }

        [HttpGet]
        [Route("/api/[controller]", Name = "GetSasKey")]
        [ProducesResponseType(typeof(Uri), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSasKeyForBlobAsync(string blobName)
        {
            var sasKey = await GetUserDelegationSasBlobUri(blobName);
            return Ok($"{sasKey}");
        }

        private async Task<Uri> GetUserDelegationSasBlobUri(string blobName)
        {

            // Construct the blob endpoint from the account name.
            var blobEndpoint = $"https://{_config["StorageAccount:Name"]}.blob.core.windows.net";

            // Create a new Blob service client with Azure AD credentials.  
            var blobClient = new BlobServiceClient(new Uri(blobEndpoint),
                                                                    AuthenticationHelper.GetTokenCredential(_config));

            // Get a user delegation key for the Blob service that's valid for 10 minutes.
            // You can use the key to generate any number of shared access signatures over the lifetime of the key.
            UserDelegationKey key = await blobClient.GetUserDelegationKeyAsync(DateTimeOffset.UtcNow,
                                                                                DateTimeOffset.UtcNow.AddMinutes(10));

            // Read the key's properties.
            _logger.LogInformation("User delegation key properties:");
            _logger.LogInformation("Key signed start: {0}", key.SignedStartsOn);
            _logger.LogInformation("Key signed expiry: {0}", key.SignedExpiresOn);
            _logger.LogInformation("Key signed object ID: {0}", key.SignedObjectId);
            _logger.LogInformation("Key signed tenant ID: {0}", key.SignedTenantId);
            _logger.LogInformation("Key signed service: {0}", key.SignedService);
            _logger.LogInformation("Key signed version: {0}", key.SignedVersion);

            // Create a SAS token that's valid for one hour.
            var sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = _config["StorageAccount:ContainerName"],
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(10)
            };

            // Specify write permissions for the SAS.
            sasBuilder.SetPermissions(BlobSasPermissions.Write);

            // Use the key to get the SAS token.
            var sasToken = sasBuilder.ToSasQueryParameters(key, _config["StorageAccount:Name"]).ToString();

            // Construct the full URI, including the SAS token.
            var fullUri = new UriBuilder()
            {
                Scheme = "https",
                Host = $"{_config["StorageAccount:Name"]}.blob.core.windows.net",
                Path = $"{_config["StorageAccount:ContainerName"]}/{blobName}",
                Query = sasToken
            };

            _logger.LogInformation("User delegation SAS URI: {0}", fullUri);

            return fullUri.Uri;
        }
    }
}
