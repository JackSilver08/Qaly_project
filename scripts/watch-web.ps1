param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$WatchArguments
)

$env:DOTNET_WATCH_SUPPRESS_BROWSER_REFRESH = "1"

$arguments = @(
    "watch"
    "run"
    "--project"
    "src/Qaly.Web"
) + $WatchArguments

& dotnet @arguments
exit $LASTEXITCODE
