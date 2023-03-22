#Requires -Module Microsoft.Graph.Applications
#Requires -Module Microsoft.Graph.Authentication
#Requires -Module Microsoft.Graph.Users

param (
    [Parameter(Mandatory=$true)]
    [string] $ApplicationName,
    [string] $AccessToken,
    [string[]] $SpaRedirectUris,
    [string[]] $WebRedirectUris,
    [string[]] $CorsUrls
)
try
{
    $FuncAppRedirectUri = "https://$($ApplicationName)func.azurewebsites.net"
    $SpaRedirectUris += $FuncAppRedirectUri

    $CorsUrls += $FuncAppRedirectUri

    Write-Host "Connecting to Graph"
    if ($token) {
        $c = Connect-MgGraph -AccessToken $token
    }
    else {
        # If running as a user not in a container you can use this command
        $c = Connect-MgGraph -Scopes "Application.ReadWrite.All", "User.ReadBasic.All"
    }

    $frontendApplication = Get-MgApplication -Filter "DisplayName eq '$($ApplicationName) Frontend'"
    if (!$frontendApplication) {
        Write-Warning -Message "Creating Frontend Application: $($ApplicationName) Frontend"
        # Create Frontend Application
        $frontendApplicationParams = @{
            DisplayName = $ApplicationName + " Frontend"
            Spa = @{
                RedirectUris = $SpaRedirectUris
            }
            Web = @{
                RedirectUris = $WebRedirectUris
            }
            SignInAudience = "AzureADMultipleOrgs"
        }
        $frontendApplication = New-MgApplication @frontendApplicationParams
    }

    $backendApplication = Get-MgApplication -Filter "DisplayName eq '$($ApplicationName) Backend'"
    if (!$backendApplication) {
        Write-Warning -Message "Creating Frontend Application: $($ApplicationName) Backend"
        # Create backnd application
        $backendApplicationParams = @{
            DisplayName = $ApplicationName + " Backend"
            SignInAudience = "AzureADMultipleOrgs"
            RequiredResourceAccess = @{
                ResourceAppId = "00000003-0000-0000-c000-000000000000" # MS Graph
                ResourceAccess = @(
                    @{
                        Id = "f501c180-9344-439a-bca0-6cbf209fd270" # Chat.Read
                        Type = "Scope"
                    }
                )
            }
        }
        $backendApplication = New-MgApplication @backendApplicationParams

        Write-Warning -Message "Adding Microsoft Graph Permissions to: $($ApplicationName) Backend"
        # Update backend application with Id needed
        # after inital creation
        $scopeId = New-Guid
        $backendScopeParams = @{
            IdentifierUris = @(
                "api://" + $backendApplication.AppId
            )
            Api = @{
                KnownClientApplications = @(
                    $frontendApplication.AppId
                )
                Oauth2PermissionScopes = @(
                    @{ 
                        Id = $scopeId; 
                        AdminConsentDescription = "Allows the app to read all 1-to-1 or group chat messages in Microsoft Teams.";
                        AdminConsentDisplayName = "Read all chat messages";
                        UserConsentDescription = "Allows an app to read 1 on 1 or group chats threads, on behalf of the signed-in user.";
                        UserConsentDisplayName = "Read user chat messages";
                        Value = "Chat.Read";
                        IsEnabled = $true;
                        Type = "User"
                    }
                )
            }
        }
        Update-MgApplication -ApplicationId $backendApplication.Id @backendScopeParams
    }

    if ($frontendApplication.RequiredResourceAccess.ResourceAppId -ne $backendApplication.AppId) {
        Write-Warning -Message "Adding $($ApplicationName) Backend api scope to $($ApplicationName) Frontend"
        # Update frontend application with backend app scope
        $frontendScopesParams = @{
            RequiredResourceAccess = @{
                ResourceAppId = $backendApplication.AppId
                ResourceAccess = @(
                    @{
                        Id = $scopeId
                        Type = "Scope"
                    }
                )
            }
        }
        Update-MgApplication -ApplicationId $frontendApplication.Id @frontendScopesParams
    }

    Write-Warning -Message "Creating new client secret for: $($ApplicationName) Backend"
    # Even if the App exists, we need to create a new secret
    # Create secret for backend application
    $backendSecretParams = @{
        PasswordCredential = @{
            DisplayName = New-Guid
        }
    }
    $backendSecret = Add-MgApplicationPassword -ApplicationId $backendApplication.Id @backendSecretParams

    # Get Graph Change Tracking SP Id
    $graphChangeTrackingSp = Get-MgServicePrincipal -Filter "AppId eq '0bf30f3b-4a52-48df-9a82-234910c4a086'"

    # Get current User Oid
    $context = Get-MgContext
    $user = Get-MgUser -Filter "UserPrincipalName eq '$($context.Account)'"

    $paramsOutput = @{
        '$schema'= 'https=//schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#'
        'contentVersion'= '1.0.0.0'
        'parameters'= @{
            'appName'= @{
                'value' = $ApplicationName
            }
            'graphChangeTrackingSpId'= @{
                'value' = $graphChangeTrackingSp.Id
            }
            'userId'= @{
                'value' = $user.Id
            }
            'apiClientId'= @{
                'value' = $backendApplication.AppId
            }
            'apiClientSecret'= @{
                'value' = $backendSecret.SecretText
            }
            'corsUrls' = @{
                'value' = $CorsUrls
            }
        }
    }

    $paramsOutput | ConvertTo-Json -Depth 5 | Set-Content './bicep/main.parameters.json'

    Write-Host "Frontend ClientId: " $frontendApplication.AppId
    Write-Host "Backend ClientId: " $backendApplication.AppId
    Write-Host "Microsft Graph Change Tracking Service Principal Id: " $graphChangeTrackingSp.Id
    Write-Host "User Account Id: " $user.Id

    return [PSCustomObject]@{
        FrontendClientId = $frontendApplication.AppId
        BackendClientId = $backendApplication.AppId
        RedirectUri = $FuncAppRedirectUri
    }
}
catch
{
    Write-Warning $_
    Write-Warning $_.exception
}