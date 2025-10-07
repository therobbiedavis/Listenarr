#!/usr/bin/env pwsh
# Listenarr Development Startup Script (PowerShell)
# This script installs dependencies and starts both frontend and backend services

Write-Host "🎵 Listenarr Development Setup" -ForegroundColor Cyan
Write-Host "==============================" -ForegroundColor Cyan
Write-Host ""

# Check if we're in the right directory
if (-not (Test-Path "package.json") -or -not (Test-Path "fe") -or -not (Test-Path "listenarr.api")) {
    Write-Host "❌ Error: Please run this script from the root Listenarr directory" -ForegroundColor Red
    exit 1
}

# Check for Node.js
try {
    $null = Get-Command node -ErrorAction Stop
} catch {
    Write-Host "❌ Error: Node.js is not installed. Please install Node.js 20.x or later" -ForegroundColor Red
    exit 1
}

# Check for .NET
try {
    $null = Get-Command dotnet -ErrorAction Stop
} catch {
    Write-Host "❌ Error: .NET SDK is not installed. Please install .NET 7.0 or later" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Prerequisites check passed" -ForegroundColor Green

# Install frontend dependencies if needed
if (-not (Test-Path "fe\node_modules")) {
    Write-Host "📦 Installing frontend dependencies..." -ForegroundColor Yellow
    Push-Location fe
    npm install
    Pop-Location
} else {
    Write-Host "✅ Frontend dependencies already installed" -ForegroundColor Green
}

# Install root dependencies for concurrently
if (-not (Test-Path "node_modules")) {
    Write-Host "📦 Installing root dependencies..." -ForegroundColor Yellow
    npm install
}

# Restore .NET dependencies
Write-Host "📦 Restoring .NET dependencies..." -ForegroundColor Yellow
Push-Location listenarr.api
dotnet restore | Out-Null
Pop-Location

Write-Host "✅ All dependencies ready" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 Starting development servers..." -ForegroundColor Green
Write-Host "   API will be available at: http://localhost:5146" -ForegroundColor Cyan
Write-Host "   Web will be available at: http://localhost:5173" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop both servers" -ForegroundColor Yellow
Write-Host ""

# Start both services
npm run dev
