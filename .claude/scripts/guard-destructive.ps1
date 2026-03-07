$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try {
    $data = $raw | ConvertFrom-Json -ErrorAction Stop
} catch {
    exit 0
}

$toolName = $data.tool_name
if ($toolName -ne 'Bash') { exit 0 }

$command = $data.tool_input.command
if (-not $command) { exit 0 }

# Strip heredoc/quoted body content to avoid false positives on text within strings
# Extract only the command structure before any heredoc (<<'EOF' or <<EOF)
$commandToCheck = ($command -split "<<'?EOF'?")[0]

# Block push directly to main or master
if ($commandToCheck -match 'git\s+push\s+\S+\s+(main|master)\b') {
    Write-Error "Blocked: cannot push directly to main/master. Use a feature branch and create a PR."
    exit 2
}

# Block destructive commands
$destructivePatterns = @(
    'rm\s+-rf',
    'git\s+reset\s+--hard',
    'git\s+checkout\s+\.\s*$',
    'git\s+clean\s+-[fd]',
    'git\s+restore\s+\.\s*$'
)

foreach ($pattern in $destructivePatterns) {
    if ($commandToCheck -match $pattern) {
        Write-Error "Blocked: destructive command detected. Reassess your approach."
        exit 2
    }
}
