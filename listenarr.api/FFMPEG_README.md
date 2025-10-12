ffprobe / ffmpeg requirement
=============================

This project uses `ffprobe` (part of the FFmpeg project) to extract accurate audio file metadata (duration, sample rate, bitrate, channels, etc.).

Please do NOT check-in or bundle FFmpeg/ffprobe binaries in this repository. Instead, install ffmpeg/ffprobe on the host system and ensure `ffprobe` is available on the PATH for the user running the Listenarr service.

Installation suggestions:

- Debian/Ubuntu:
  sudo apt update
  sudo apt install ffmpeg
- macOS (Homebrew):
  brew install ffmpeg
- Windows (chocolatey / winget):
  choco install ffmpeg
  # or
  winget install ffmpeg

Licensing
---------
FFmpeg is distributed under the LGPL or GPL depending on how it is built. If you link or distribute FFmpeg binaries as part of your product, be sure to comply with the FFmpeg licensing terms (LGPL/GPL) and any third-party codec licensing requirements. By default, this project does not redistribute FFmpeg — it expects system-installed ffprobe.

If you need a managed or NuGet-based approach, consider adding a wrapper library (e.g., Xabe.FFmpeg) and follow its distribution/licensing guidance. Note that many NuGet packages still require FFmpeg binaries at runtime.
