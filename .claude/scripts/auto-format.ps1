$data = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $data.tool_input.file_path
if ($path) {
    dotnet format --include $path --no-restore --verbosity quiet
}
