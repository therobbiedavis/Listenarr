#!/bin/bash

# Listenarr Development Startup Script
# This script installs dependencies and starts both frontend and backend services

echo "🎵 ListenArr Development Setup"
echo "=============================="

# Check if we're in the right directory
if [ ! -f "package.json" ] || [ ! -d "fe" ] || [ ! -d "listenarr.api" ]; then
    echo "❌ Error: Please run this script from the root Listenarr directory"
    exit 1
fi

# Install root dependencies for concurrently
if [ ! -d "node_modules" ]; then
    echo "📦 Installing root dependencies..."
    npm install
fi

echo "✅ All dependencies ready"
echo ""
echo "🚀 Starting development servers..."
echo "   API will be available at: http://localhost:5146"
echo "   Web will be available at: http://localhost:5173"
echo ""
echo "Press Ctrl+C to stop both servers"
echo ""

# Start both services
npm run devript installs dependencies and starts both frontend and backend services

echo ""
echo ""
echo "🎵 Listenarr Development Setup"
echo "=============================="

# Check if we're in the right directory
if [ ! -f "package.json" ] || [ ! -d "fe" ] || [ ! -d "ListenArr.Api" ]; then
    echo "❌ Error: Please run this script from the root Listenarr directory"
    exit 1
fi

# Check for Node.js
if ! command -v node &> /dev/null; then
    echo "❌ Error: Node.js is not installed. Please install Node.js 20.x or later"
    exit 1
fi

# Check for .NET
if ! command -v dotnet &> /dev/null; then
    echo "❌ Error: .NET SDK is not installed. Please install .NET 7.0 or later"
    exit 1
fi

echo "✅ Prerequisites check passed"

# Install frontend dependencies if needed
if [ ! -d "fe/node_modules" ]; then
    echo "📦 Installing frontend dependencies..."
    cd fe
    npm install
    cd ..
else
    echo "✅ Frontend dependencies already installed"
fi

# Restore .NET dependencies
echo "📦 Restoring .NET dependencies..."
cd ListenArr.Api
dotnet restore > /dev/null 2>&1
cd ..

echo "✅ All dependencies ready"
echo ""
echo "🧹 Cleaning all bin/obj folders for a fresh build..."
find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
echo "� Rebuilding .NET solution..."
echo "Building backend..."
echo ""

dotnet build listenarr.sln
echo "�🚀 Starting development servers..."
echo "   API will be available at: http://localhost:5146"
echo "   Web will be available at: http://localhost:5173"
echo ""
echo "Press Ctrl+C to stop both servers"
echo ""

# Start both services with permissive CORS for development
export ASPNETCORE_ENVIRONMENT=Development
export LISTENARR_CORS_POLICY=DevAll
npm run dev:full