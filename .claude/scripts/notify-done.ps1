# Notification hook (idle_prompt): audible "ready for you" signal when Claude hands control back.
# Side-effect only — never blocks. Plays the Windows Asterisk sound (respects the user's sound scheme).
$null = [Console]::In.ReadToEnd()
try { [System.Media.SystemSounds]::Asterisk.Play() } catch { }
