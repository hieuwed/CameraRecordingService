# FFmpeg Installation Guide

## Cài đặt FFmpeg cho Windows

Ứng dụng cần FFmpeg để encode video sang định dạng MP4 với H.265 codec.

### Cách 1: Sử dụng Chocolatey (Khuyến nghị)

```powershell
# Mở PowerShell với quyền Administrator
choco install ffmpeg
```

### Cách 2: Tải thủ công

1. Tải FFmpeg từ: https://www.gyan.dev/ffmpeg/builds/
2. Chọn "ffmpeg-release-essentials.zip"
3. Giải nén vào `C:\ffmpeg`
4. Thêm `C:\ffmpeg\bin` vào PATH:
   - Mở "Environment Variables"
   - Thêm `C:\ffmpeg\bin` vào System PATH
   - Restart terminal

### Kiểm tra cài đặt

```powershell
ffmpeg -version
```

Nếu thấy version info là đã cài thành công!

## Tại sao cần FFmpeg?

- **H.265/HEVC codec**: Tiết kiệm 50% dung lượng so với H.264
- **MP4 format**: Tương thích với mọi thiết bị và player
- **Tối ưu cho video dài**: 45 phút - vài tiếng chỉ vài trăm MB

## Ước tính dung lượng

Với H.265 @ 2000kbps:
- 45 phút: ~675 MB
- 1 tiếng: ~900 MB  
- 2 tiếng: ~1.8 GB

So với MJPEG/AVI sẽ tiết kiệm 60-70% dung lượng!
