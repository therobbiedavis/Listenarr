$DbPath = 'c:\Users\Robbie\Documents\GitHub\Listenarr\listenarr.api\config\database\listenarr.db'
$JobId = '8278b333-22de-4f32-9a2a-d533e5afc901'
$PrevStatus = -1
$LogPath = 'C:\Users\Robbie\listenarr_api.log'
$LastLogLength = 0

for ($i=0; $i -lt 100; $i++) {
    try {
        $conn = New-Object System.Data.SQLite.SQLiteConnection "Data Source=$DbPath"
        $conn.Open()
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT Status,StartedAt,CompletedAt,ProcessingLog,SourcePath,DestinationPath FROM DownloadProcessingJobs WHERE Id = '$JobId'"
        $r = $cmd.ExecuteReader()
        if ($r.Read()) {
            $st = $r.GetInt32(0)
            $started = if ($r.IsDBNull(1)) {''} else {$r.GetDateTime(1)}
            $completed = if ($r.IsDBNull(2)) {''} else {$r.GetDateTime(2)}
            $log = if ($r.IsDBNull(3)) {''} else {$r.GetString(3)}
            $src = if ($r.IsDBNull(4)) {''} else {$r.GetString(4)}
            $dst = if ($r.IsDBNull(5)) {''} else {$r.GetString(5)}

            if ($st -ne $PrevStatus) {
                Write-Output "[$(Get-Date -Format o)] Job status changed: $PrevStatus -> $st (StartedAt=$started CompletedAt=$completed)"
                Write-Output "Source: $src"
                Write-Output "Destination: $dst"
                if ($log) { Write-Output "ProcessingLog (truncated 1000 chars):"; Write-Output ($log.Substring(0,[Math]::Min(1000,$log.Length))) }
                $PrevStatus = $st
            }
        } else {
            Write-Output "Job not found"
        }
        $conn.Close()
    } catch {
        Write-Output "Error querying DB: $_"
    }

    if (Test-Path $LogPath) {
        try {
            $content = Get-Content -Path $LogPath -Raw -ErrorAction Stop
            if ($content.Length -gt $LastLogLength) {
                $new = $content.Substring($LastLogLength)
                Write-Output "=== New log lines appended ==="
                Write-Output $new
                $LastLogLength = $content.Length
            }
        } catch {
            Write-Output "Error reading log file: $_"
        }
    }

    Start-Sleep -Seconds 3
}

Write-Output "Polling finished."