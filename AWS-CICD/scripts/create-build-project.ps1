# Get input parameters
param (
    [string]$awsProfileName = $(throw "-awsProfileName is required.")
)

Set-AWSCredential -ProfileName $awsProfileName

$config = Get-Content -Path config.json | ConvertFrom-Json

# Get the script location
$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$projectsPath = [System.IO.Path]::Combine($scriptPath, "..\CodeBuild\projects")

$input = [System.IO.File]::ReadAllText([System.IO.Path]::Combine($projectsPath, "build.json"))
$input = $input.Replace("<aws_account>", $config.aws_account).Replace("<code_build_service_role_arn>", $config.code_build_service_role_arn)

$tempFile = New-TemporaryFile
$input | Out-File -Encoding ASCII -NoNewline $tempFile

aws codebuild create-project --cli-input-json file://$tempFile --profile $awsProfileName

Remove-Item -Path $tempFile