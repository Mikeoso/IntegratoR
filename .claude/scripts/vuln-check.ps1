# PostToolUse(Write|Edit) hook: advisory vulnerable-package scan, but ONLY when Directory.Packages.props
# is edited (the single file where dependency versions change under Central Package Management).
# Advisory only — never blocks. The authoritative gate is CI: .github/workflows/build.yml.
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { return }

try {
    $data = $raw | ConvertFrom-Json -ErrorAction Stop
} catch {
    return
}

$path = $data.tool_input.file_path
if (-not $path) { return }
if ($path -notlike '*Directory.Packages.props') { return }   # only the CPM versions file
if ($path -like '*worktrees*') { return }                    # ignore stale .claude/worktrees copies

$root = Split-Path -Parent $path
$sln = Join-Path $root 'IntegratoR.sln'
$target = if (Test-Path $sln) { $sln } else { $root }

$out = dotnet list $target package --vulnerable --include-transitive 2>&1
if ($out -match 'has the following vulnerable packages') {
    Write-Output "[vuln-check] Vulnerable package(s) detected (advisory; CI gates this at build.yml). Review before pushing:"
    $out | Select-String 'vulnerable|>' | ForEach-Object { Write-Output $_.Line }
}
