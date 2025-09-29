#!/bin/bash

# Thumbnail Generator - Full Stack Startup Script

echo "🚀 Starting Thumbnail Generator (API + React Frontend)"
echo "=================================================="

# Check if .NET is available
if ! command -v dotnet &> /dev/null; then
    echo "❌ .NET SDK not found. Please install .NET 8 SDK first."
    exit 1
fi

# Check if Node.js is available
if ! command -v node &> /dev/null; then
    echo "❌ Node.js not found. Please install Node.js first."
    exit 1
fi

# Set environment variables for development
export AES_KEY_BASE64=$(openssl rand -base64 32)
export AES_IV_BASE64=$(openssl rand -base64 16)
export ASPNETCORE_ENVIRONMENT=Development

echo "🔧 Environment variables set:"
echo "   - AES_KEY_BASE64: Generated"
echo "   - AES_IV_BASE64: Generated"
echo "   - ASPNETCORE_ENVIRONMENT: Development"

# Start the API in background
echo ""
echo "🌐 Starting .NET API server..."
cd ThumbnailService
dotnet run --urls="http://localhost:5001" &
API_PID=$!

# Wait a moment for API to start
sleep 3

# Start the React frontend
echo ""
echo "⚛️  Starting React frontend..."
cd ../frontend
npm start &
FRONTEND_PID=$!

echo ""
echo "✅ Both servers are starting..."
echo "   - API: http://localhost:5001"
echo "   - Frontend: http://localhost:3000"
echo "   - Swagger: http://localhost:5001/swagger"
echo ""
echo "Press Ctrl+C to stop both servers"

# Function to cleanup on exit
cleanup() {
    echo ""
    echo "🛑 Stopping servers..."
    kill $API_PID 2>/dev/null
    kill $FRONTEND_PID 2>/dev/null
    echo "✅ Servers stopped"
    exit 0
}

# Set trap to cleanup on script exit
trap cleanup SIGINT SIGTERM

# Wait for both processes
wait
