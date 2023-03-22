
# Deployment

We recommend deploying this solution from a [devcontainer](https://code.visualstudio.com/docs/remote/create-dev-container)
or [GitHub CodeSpace](https://github.com/features/codespaces) for the easiest experience.

1. Open the repo in a GitHub CodeSpace or open it in a container with VSCode
    * All prerequisites will be installed automatically
1. Open up the terminal (ctrl + J) or click on the deploy.ps1 file
1. Run the `deploy.ps1` script

    * In the terminal type `./deploy.ps1` and hit enter
    * Or with the file open, click the play button on the top right

1. The script will prompt you for a unique ApplicationName and a Resource Group Name
    * Example: `./deploy.ps1 -ApplicationName gnbdemo -ResourceGroupName gnbdemo Location eastus2`
    * Optional Parameters: You can add any of these optional parameters when executing the deploy.ps1 script. TenantId and SubscriptionId are used during the sign in process to choose the correct tenant and subscription if you have access to multiple.
        - TenantId
        - SubscriptionId

## What does the deployment do?

1. Azure PowerShell prompts you to login
1. [Script] Create the Azure AD Application Registrations
    * Creates a Frontend App Registration
    * Creates a Backend App Registration
    * Creates a Client Secret for the Backend Application
    * Creates a `main.parameter.json` file in the bicep folder needed for the deployment
    * Outputs information needed for the Test Client
1. [Script] Set Test Client Config: This script replaces the token placeholders
with the identifiers needed for the SPA App
1. Create the Resource Group if it doesn't already exist
1. [Bicep] Deploy the resources to Azure
1. Deploy the Azure Function Code to the Function App
1. [Bicep] Update the SignalR resource with the upstream URL from the Azure Function
    * This needs to happen after the function code has been deployed because the SignalR Function Key is created after the code is deployed

## Manual Deployment

### Prerequisites

1. Install [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7)
1. Install Azure PowerShell [Azure PowerShell](https://docs.microsoft.com/en-us/powershell/azure/install-az-ps?view=azps-8.2.0)
1. Install [Bicep](https://docs.microsoft.com/en-us/azure/azure-resource-manager/bicep/install)
1. Install [Microsoft Graph PowerShell Module](https://docs.microsoft.com/en-us/powershell/microsoftgraph/installation?view=graph-powershell-1.0)
1. Install [Azure Functions Core Tools](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)

Refer to deployment steps above to deploy.
NOTE: If not deploying from within a container, the DeviceAuthentication
flag can be removed and will fallback to interactive authentication.

## Testing Graph Notification Broker

In order to test the Graph Notification Broker a Microsoft 365 Tenant is required. This Microsoft 365 can be associated to any Azure AD tenant as the Graph Notification Broker is developed to work in a multi tenant manner.

For Microsoft Internal:</br>
A temporary Microsoft 365 tenant can be requested with demo data at <https://cdx.transform.microsoft.com/my-tenants>.

For non Microsoft:</br>
A trial Microsoft 365 tenant can be requested at <https://developer.microsoft.com/en-us/microsoft-365/dev-program>. This will contain 1 licensed user, but a chat with external users can be setup and used for the test of GNB.

Once a tenant is available you can start testing the Graph Notification Broker using the available test application. This application is available using the the URL of the function app that is part of the deployment of the Graph Notification Broker.

To find the URL go to the azure portal, go to the resource group that was created for the graph notification broker and find the function app. This should have the name <appname>func, and URL in the form of <https://appnamefunc.azurewebsites.net>.

### Test application

 With the demo application a setup a chat with one of the demo users and use that chat to test the subscription broker.

 ![test app](/images/screenshot%20test%20app.png)

 After signing in a consent is required to allow the application to access the Graph API on behalf of the user.

 ![consent page](/images/consent%20page.png)

 After consent the page will be loaded and a subscription can be made. For this open a new tab and open <https://teams.microsoft.com> and go or create a chat with another user. Copy the id of the chat

 ![chat id](/images/team%20chat%20id.png)

Go back to the test application and add the chat id between the chats/ and /messages. By doing this you have created the resource location for the chat that will be used to create a subscription. After this click on subscribe and a subscription will be make (takes about 15 seconds). The subscription will be shown in the section Existing Subscriptions.

A notification can be shown by going to the chat in teams and enter a message. After a few seconds a notification with the text you entered should appear.

![complete test](/images/complete%20test.png)
