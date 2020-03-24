// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------
import { BaseRequestPolicy } from "@azure/storage-blob";

export default class SasUpdatePolicyFactory {
  constructor(sasStore) {
    this.sasStore = sasStore;
  }
  create(nextPolicy, options) {
    return new SasUpdatePolicy(nextPolicy, options, this.sasStore);
  }
}

class SasUpdatePolicy extends BaseRequestPolicy {
  constructor(nextPolicy, options, sasStore) {
    super(nextPolicy, options);
    this.sasStore = sasStore;
  }

  async sendRequest(request) {
    const urlObj = new URL(request.url);
    const sas = await this.sasStore.getValidSASForBlob(
      `${urlObj.origin}${urlObj.pathname}`
    );
    new URL(`${sas}`).searchParams.forEach((value, key) => {
      urlObj.searchParams.set(key, value);
    });

    // Update request URL with latest SAS
    request.url = urlObj.toString();

    return this._nextPolicy.sendRequest(request);
  }
}
