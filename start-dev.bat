@echo off
REM Lif not exist "listenarr.api" (
    echo ❌ Error: Please run this script from the root Listenarr directory
    exit /b 1
)nArr Development Startup Script
REM This script installs dependencies and starts both frontend and backend services

echo 🎵 ListenArr Development Setup
echo ==============================

REM Check if we're in the right directory
if not exist "package.json" (
    echo ❌ Error: Please run this script from the root Listenarr directory
    exit /b 1
)
if not exist "fe" (
    echo ❌ Error: Please run this script from the root Listenarr directory
    exit /b 1
)
if not exist "ListenArr.Api" (
    echo ❌ Error: Please run this script from the root Listenarr directory
    exit /b 1
)

REM Check for Node.js
where node >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Error: Node.js is not installed. Please install Node.js 20.x or later
    exit /b 1
)

REM Check for .NET
where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo ❌ Error: .NET SDK is not installed. Please install .NET 7.0 or later
    exit /b 1
)

echo ✅ Prerequisites check passed

REM Install frontend dependencies if needed
if not exist "fe\node_modules" (
    echo 📦 Installing frontend dependencies...
    cd fe
    call npm install
    cd ..
) else (
    echo ✅ Frontend dependencies already installed
)

REM Restore .NET dependencies
echo 📦 Restoring .NET dependencies...
cd ListenArr.Api
dotnet restore >nul 2>nul
cd ..

REM Install root dependencies for concurrently
if not exist "node_modules" (
    echo 📦 Installing root dependencies...
    call npm install
)

echo ✅ All dependencies ready
echo.
echo 🚀 Starting development servers...
echo    API will be available at: http://localhost:5146
echo    Web will be available at: http://localhost:5173
echo.
echo Press Ctrl+C to stop both servers
echo.

REM Start both services
npm run dev