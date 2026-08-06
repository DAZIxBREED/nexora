$ErrorActionPreference = "Stop"
$Packages = Get-ChildItem "Packages" -Directory -Filter "com.nexora.*"
if ($Packages.Count -eq 0) { throw "No Nexora packages found" }
foreach ($Package in $Packages) {
    $Data = Get-Content (Join-Path $Package.FullName "package.json") -Raw | ConvertFrom-Json
    if ($Data.name -ne $Package.Name) { throw "Package name mismatch: $($Package.Name)" }
}
Write-Host "Validated $($Packages.Count) Nexora packages"
