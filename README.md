# Azure Cognitive Services Reference Implementation

This reference implementation builds the first phase of a call center analytics pipeline. Customer calls (saved as audio files) are batch processed wherein the audio speech is transcribed to text and stored in a blob storage container.

<a href="https://shell.azure.com" title="Launch Azure Cloud Shell"><img name="launch-cloud-shell" src="https://docs.microsoft.com/azure/includes/media/cloud-shell-try-it/launchcloudshell.png" /></a>

The deployment steps shown here use Bash shell commands. On Windows, you can use the [Windows Subsystem for Linux](https://docs.microsoft.com/windows/wsl/about) to run Bash.

## Prerequisites

- Azure subscription
- [Azure Function Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local#v3)
- (Optional) [Azure Storage Explorer](https://azure.microsoft.com/en-us/features/storage-explorer/) for local testing and viewing the contents of the remote/local storage folders containing speech and transcribed data.

## Clone this repo locally

```bash
git clone https://github.com/mspnp/cognitive-services-reference-implementation.git my-local-folder && \
cd my-local-folder/src
```

If you have multiple Azure subscriptions, make sure that the subscription that you want to use for this deployment is set as default. Login to your Azure account and set the subscription.

```bash
az login
az account set -s [YOUR_SUBSCRIPTION_ID]
```

## Deploy the Cognitive Services Reference Implementation

### Deploy an Azure Key Vault for storing secrets

#### Set environment variables

```bash
export KEY_VAULT_RESOURCE_GROUP_NAME=[YOUR_KEYVAULT_RESOURCE_GROUP_NAME]
export LOCATION=[YOUR_LOCATION_HERE]
export USER_EMAIL=[YOUR_USER_EMAIL]
export KEYVAULT_TEMPLATE_FILE=./azuredeploy-keyvault.json
export KEYVAULT_NAME=[YOUR_KEYVAULT_NAME]
```

#### Get the object ID of the current user or a specific user

```bash
OBJECT_ID=$(az ad user show --id $USER_EMAIL --query objectId --output tsv)
```

#### Get the tenant ID for your subscription

```bash
TENANT_ID=$(az account show | jq -r '.tenantId')
```

#### Create a resource group in the specified region

```bash
az group create -n $KEY_VAULT_RESOURCE_GROUP_NAME -l $LOCATION
```

#### Deploy key vault.

```bash
az deployment group create \
    -g $KEY_VAULT_RESOURCE_GROUP_NAME \
    --template-file $KEYVAULT_TEMPLATE_FILE \
    --parameters \
        keyVaultName=$KEYVAULT_NAME \
        tenantId=$TENANT_ID \
        objectId=$OBJECT_ID
```

### Deploy Cognitive Services

#### Create a service principal for user delegation SAS token

```bash
export SP_DETAILS=$(az ad sp create-for-rbac -n "speech-uploader-sp" --skip-assignment -o json) && \
export SP_APP_ID=$(echo $SP_DETAILS | jq ".appId" -r) && \
export SP_CLIENT_SECRET=$(echo $SP_DETAILS | jq ".password" -r) && \
export SP_OBJECT_ID=$(az ad sp show --id $SP_APP_ID -o tsv --query objectId)
```

#### Deploy cognitive services deployment template

```bash
export RESOURCE_GROUP_NAME=[YOUR_RESOURCE_GROUP_NAME]
export TEMPLATE_FILE=./azuredeploy.json
export COGNITIVE_SERVICES_ACCOUNT_NAME=[YOUR_COGNITIVE_SERVICES_ACCOUNT_NAME]
export FUNCTION_APP_NAME=[YOUR_FUNCTION_APP_NAME]
export WEB_APP_NAME=[YOUR_WEB_APP_NAME]

az group create -n $RESOURCE_GROUP_NAME -l $LOCATION

az deployment group create \
    -g $RESOURCE_GROUP_NAME \
    --template-file $TEMPLATE_FILE \
    --parameters \
        cognitiveServicesAccountName=$COGNITIVE_SERVICES_ACCOUNT_NAME \
        keyVaultResourceGroupName=$KEY_VAULT_RESOURCE_GROUP_NAME \
        keyVaultName=$KEYVAULT_NAME \
        sitesCognitiveFuncPipelineName=$FUNCTION_APP_NAME \
        sitesTokenApiServiceName=$WEB_APP_NAME \
        existingServicePrincipalAppId=$SP_OBJECT_ID \
        existingServicePrincipalTenantId=$SP_APP_ID \
        existingServicePrincipalAppPassword=$SP_CLIENT_SECRET
```

#### Build and deploy function app

```bash
cd ./infra \
&& func pack --build-native-deps \
&& az functionapp deployment source config-zip \
      -g $RESOURCE_GROUP_NAME \
      -n $FUNCTION_APP_NAME \
      --src ./infra.zip \
&& cd -
```

#### Run the SAS token based uploader application

```bash
cd ./jsloader
npm run build
npm start
```
