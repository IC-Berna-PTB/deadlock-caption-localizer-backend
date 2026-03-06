#!/bin/env python
import subprocess
from zipfile import ZipFile
def main():
    subprocess.run(["dotnet", "clean"])
    [subprocess.run(["dotnet", "publish", "-c", "Release", "-r", platform, "-p:PublishSingleFile=true"]) for platform in ["linux-x64", "win-x64", "osx-arm64"]]
    with ZipFile("deadlock-audio.zip", 'w') as zip:
        zip.write("deadlock-caption-localizer-backend/bin/Release/net10.0/linux-x64/publish/deadlock-caption-localizer-backend", "backend-linux")
        zip.write("deadlock-caption-localizer-backend/bin/Release/net10.0/osx-arm64/publish/deadlock-caption-localizer-backend", "backend-macos")
        zip.write("deadlock-caption-localizer-backend/bin/Release/net10.0/win-x64/publish/deadlock-caption-localizer-backend.exe", "backend-windows.exe")
        zip.write("ff-extension.xpi")
if __name__ == "__main__":
    main()
