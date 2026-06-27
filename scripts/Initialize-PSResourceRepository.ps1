[CmdletBinding()]
param (
    [Parameter()]
    [switch]
    $InstallPslrm
)

# NOTE: This is a workaround for PSResourceGet failing to load the repository store, which causes the following error:
# Cannot retrieve the dynamic parameters for the cmdlet. Loading repository store failed: Could not find a part of the path
# '/home/runner/.local/share/PSResourceGet/PSResourceRepository.xml'.
Get-PSResourceRepository -Name PSGallery
Set-PSResourceRepository -Name PSGallery -Trusted -ApiVersion V2
if ($InstallPslrm) {
    Install-PSResource pslrm -Prerelease -Quiet -Reinstall -Scope CurrentUser
}
