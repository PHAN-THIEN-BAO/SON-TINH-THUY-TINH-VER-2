# Script để fix Unity freeze - Xóa thư mục Library
# Chạy script này khi đã ĐÓNG Unity Editor

Write-Host "=== Unity Freeze Fix Script ===" -ForegroundColor Cyan
Write-Host ""

$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$libraryPath = Join-Path $projectPath "Library"
$tempPath = Join-Path $projectPath "Temp"

# Kiểm tra xem Unity có đang chạy không
$unityProcess = Get-Process -Name "Unity" -ErrorAction SilentlyContinue
if ($unityProcess) {
    Write-Host "CẢNH BÁO: Unity đang chạy!" -ForegroundColor Red
    Write-Host "Vui lòng đóng Unity trước khi chạy script này." -ForegroundColor Yellow
    Write-Host ""
    $response = Read-Host "Bạn có muốn force close Unity không? (Y/N)"
    if ($response -eq "Y" -or $response -eq "y") {
        Stop-Process -Name "Unity" -Force
        Write-Host "Đã đóng Unity." -ForegroundColor Green
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Script đã hủy. Vui lòng đóng Unity và chạy lại." -ForegroundColor Yellow
        pause
        exit
    }
}

Write-Host "Bước 1: Xóa thư mục Library..." -ForegroundColor Yellow
if (Test-Path $libraryPath) {
    try {
        Remove-Item $libraryPath -Recurse -Force
        Write-Host "✓ Đã xóa thư mục Library" -ForegroundColor Green
    } catch {
        Write-Host "✗ Lỗi khi xóa Library: $_" -ForegroundColor Red
    }
} else {
    Write-Host "Thư mục Library không tồn tại" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Bước 2: Xóa thư mục Temp..." -ForegroundColor Yellow
if (Test-Path $tempPath) {
    try {
        Remove-Item $tempPath -Recurse -Force
        Write-Host "✓ Đã xóa thư mục Temp" -ForegroundColor Green
    } catch {
        Write-Host "✗ Lỗi khi xóa Temp: $_" -ForegroundColor Red
    }
} else {
    Write-Host "Thư mục Temp không tồn tại" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== HOÀN THÀNH ===" -ForegroundColor Green
Write-Host ""
Write-Host "Bây giờ hãy:" -ForegroundColor Cyan
Write-Host "1. Mở Unity Hub" -ForegroundColor White
Write-Host "2. Mở project này từ Unity Hub" -ForegroundColor White
Write-Host "3. Đợi Unity import lại tất cả assets (có thể mất 5-10 phút)" -ForegroundColor White
Write-Host ""
pause
