$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { return }

try {
    $data = $raw | ConvertFrom-Json -ErrorAction Stop
} catch {
    return
}

$path = $data.tool_input.file_path
if ($path) {
    dotnet format --include "$path" --no-restore --verbosity quiet
}
