// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT Licence.

/**
 * Here we demonstrate how to inject new SAS for an ongoing upload.
 * SAS might get expired before a large upload finishes, for this scenario, we want to request a new
 * SAS token during the upload instead of starting a new upload.
 *
 * In this sample, we give a SAS injection sample for browsers like Chrome which supports await/async.
 *
 * Before executing the sample:
 * - Make sure storage account has CORS set up properly
 * - Implement method `getNewSasForBlob`
 * - Update url in `upload()` method
 *
 * This sample creates a global function called `upload` that will upload
 * data from a file upload form. For example, the following HTML will create
 * such a form.
 *
 * <form><input type="file" id="file" /></form>
 * <button id="upload" onclick="upload()">Upload</button>
 *
 * For instructions on building this sample for the browser, refer to
 * "Building for Browsers" in the readme.
 *
 *
 */
import {
  BlockBlobClient,
  AnonymousCredential,
  newPipeline
} from "@azure/storage-blob";
import SasStore from "./SasStore";
import SasUpdatePolicyFactory from "./SasUpdatePolicyFactory";

export async function upload() {
  const sasStore = new SasStore();

  const pipeline = newPipeline(new AnonymousCredential());

  // Inject SAS update policy factory into current pipeline
  pipeline.factories.unshift(new SasUpdatePolicyFactory(sasStore));

  const file = document.getElementById("file").files[0];

  const restApiUrl = `https://<your-sas-token-generator-rest-api-FQDN>/api/AudioFilesUpload?blobName=${file.name}`;
  const blobUrl = "hitherto-unknown";
  const blockBlobClient = new BlockBlobClient(
    `${await sasStore.getValidSASForBlob(blobUrl, restApiUrl)}`, // A SAS should start with "?"
    pipeline
  );

  await blockBlobClient.uploadBrowserData(file, {
    maxSingleShotSize: 4 * 1024 * 1024
  });
}
