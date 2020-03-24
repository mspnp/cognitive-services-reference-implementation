// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
export default class SasStore {
  constructor() {
    this.sasCache = {};
  }

  // Get a valid SAS for blob
  async getValidSASForBlob(blobUrl, restApiUrl) {
    if (
      this.sasCache[blobUrl] &&
      this.isSasStillValidInNext2Mins(this.sasCache[blobUrl])
    ) {
      return this.sasCache[blobUrl];
    } else {
      const blobSasUrl = await this.getNewSasUrlForBlob(restApiUrl);
      const urlObj = new URL(blobSasUrl);
      blobUrl =  `${urlObj.origin}${urlObj.pathname}`;
      return (this.sasCache[blobUrl] = blobSasUrl);
    }
  }

  // Return true if "se" section in SAS is still valid in next 2 mins
  isSasStillValidInNext2Mins(sas) {
    const expiryStringInSas = new URL(`${sas}`).searchParams.get("se");
    return new Date(expiryStringInSas) - new Date() >= 2 * 60 * 1000;
  }

  // Get a new SAS for blob, we assume a SAS starts with a "?"
  async getNewSasUrlForBlob(restApiUrl) {
    // TODO: You need to implement this
    try {
      const response = await fetch(restApiUrl);
      const sasUri = await response.text();
      console.log("Text: " + sasUri);
      console.log("Status: " + response.status);
      return  sasUri;
    } catch (err) {
      console.error(err.message);
    }
  }
}
