param (
    [Parameter(Mandatory=$true)]
    [ValidateLength(3,14)]
    [string]$ApplicationName,
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    [Parameter(Mandatory=$true)]
    [string]$Location,
    [string[]] $SpaRedirectUris,
    [string[]] $WebRedirectUris,
    [string[]] $CorsUrls,
    [string] $TenantId,
    [string] $SubscriptionId
)
try
{
    # Remove non alphanumeric characters from Application Name
    $ApplicationName = $ApplicationName -replace '[^a-zA-Z0-9]', ''
    Write-Host "Starting Deployment for ${ApplicationName}"
    $c = Get-AzContext

    if (!$c) {
        # We must connect using the Device Authentication flow if running
        # from a devcontainer
        # If running from a local machine, you can remove -UseDeviceAuthentication flag
        $TenantParams = @{ }
        if ($TenantId)
        {
            $TenantParams = @{
                Tenant = $TenantId
            }
        }

        $SubscriptionParams = @{ }
        if ($SubscriptionId)
        {
            $SubscriptionParams = @{
                SubscriptionId = $SubscriptionId
            }
        }

        Write-Host @TenantParams @SubscriptionParams
        $c = Connect-AzAccount @TenantParams @SubscriptionParams -UseDeviceAuthentication | ForEach-Object Context
    }

    if ($c)
    {
        Write-Host "`nContext is: "
        $c | Select-Object Account, Subscription, Tenant, Environment | Format-List | Out-String
        
        $token = (Get-AzAccessToken -ResourceTypeName MSGraph).token

        # Create the App Registrations
        $output = ./Scripts/CreateAppRegistrations.ps1 `
            -ApplicationName $ApplicationName `
            -AccessToken $token `
            -SpaRedirectUris $SpaRedirectUris `
            -WebRedirectUris $WebRedirectUris `
            -CorsUrls $CorsUrls `
            -Verbose

        # Set the client ids in the test client
        $output | ./Scripts/SetTestClientConfig.ps1

        if (!(Get-AzResourceGroup -Name $ResourceGroupName -ErrorAction SilentlyContinue)) {
            New-AzResourceGroup -Name $ResourceGroupName -Location $Location -ErrorAction stop
        }

        # Deploy the resources
        New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile ./bicep/main.bicep -TemplateParameterFile ./bicep/main.parameters.json -Verbose

        # Deploy the Azure function code
        func azure functionapp publish "$($ApplicationName)func" --csharp

        # Configure the SignalR Upstream
        New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile ./bicep/configDeployment.bicep -TemplateParameterFile ./bicep/main.parameters.json -Verbose
    }
    else {
        throw 'Cannot get a context. Run `Connect-AzAccount -UseDeviceAuthentication`'
    }
}
catch {
    Write-Warning $_
    Write-Warning $_.exception
    Write-Warning -Message "Logging Out user"
    Logout-AzAccount
}