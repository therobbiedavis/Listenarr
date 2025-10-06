# Test script to verify search functionality
Write-Host "Testing Listenarr Search API..." -ForegroundColor Green

try {
    $response = Invoke-RestMethod -Uri "http://localhost:5146/api/search?query=fourth%20wing" -Method GET -ContentType "application/json"
    Write-Host "Search successful!" -ForegroundColor Green
    Write-Host "Results found: $($response.Length)" -ForegroundColor Yellow
    
    if ($response.Length -gt 0) {
        Write-Host "First result:" -ForegroundColor Cyan
        Write-Host "Title: $($response[0].title)" -ForegroundColor White
        Write-Host "Source: $($response[0].source)" -ForegroundColor White
        Write-Host "URL: $($response[0].url)" -ForegroundColor White
    } else {
        Write-Host "No results found - this indicates the XPath selectors may still need adjustment" -ForegroundColor Red
    }
} catch {
    Write-Host "Error testing search: $($_.Exception.Message)" -ForegroundColor Red
}