$projectPath = "c:\widgit-selfdev\prg1\FloatingPomodoro\FloatingPomodoro.csproj"
if (Test-Path $projectPath) {
    Write-Host "Project file found."
    try {
        dotnet build $projectPath
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Build Successful!"
        } else {
            Write-Host "Build Failed (Expected if SDK is missing)."
        }
    } catch {
        Write-Host "Error running dotnet build."
    }
} else {
    Write-Host "Project file not found!"
}
