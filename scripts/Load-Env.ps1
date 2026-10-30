param(
    [string]$EnvFile = (Join-Path $PSScriptRoot "..\.env")
)

if (-not (Test-Path -LiteralPath $EnvFile)) {
    throw "Environment file was not found: $EnvFile. Copy .env.example to .env first."
}

Get-Content -LiteralPath $EnvFile | ForEach-Object {
    $line = $_.Trim()
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
        return
    }

    $separator = $line.IndexOf("=")
    if ($separator -lt 1) {
        throw "Invalid environment line: $line"
    }

    $name = $line.Substring(0, $separator).Trim()
    $value = $line.Substring($separator + 1)
    if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    [Environment]::SetEnvironmentVariable($name, $value, "Process")
}

Write-Output "Environment variables loaded from $EnvFile"
