#!/bin/bash

# Start backend in background
echo "Starting backend API server..."
cd listenarr.api
dotnet run &ll any existing processes on the ports
echo "Cleaning up existing processes..."
lsof -ti:5146 | xargs kill -9 2>/dev/null || true
lsof -ti:5173 | xargs kill -9 2>/dev/null || true

# Navigate to project root
cd "$(dirname "$0")"

echo "Starting Listenarr Development Servers..."
echo "=================================="

# Start backend in background
echo "Starting backend API server..."
cd ListenArr.Api
dotnet run &
BACKEND_PID=$!
cd ..

# Wait a moment for backend to start
sleep 3

# Start frontend
echo "Starting frontend..."
cd fe
./node_modules/.bin/vite --port 5173 --host &
FRONTEND_PID=$!
cd ..

echo "=================================="
echo "✅ Backend API: http://localhost:5146"
echo "✅ Frontend Web: http://localhost:5173" 
echo "✅ Modal Demo: http://localhost:8080"
echo "=================================="
echo "Press Ctrl+C to stop all servers"

# Function to cleanup when script exits
cleanup() {
    echo "Stopping all servers..."
    kill $BACKEND_PID 2>/dev/null || true
    kill $FRONTEND_PID 2>/dev/null || true
    exit 0
}

# Set trap to cleanup on script exit
trap cleanup SIGINT SIGTERM

# Wait for user to stop
wait