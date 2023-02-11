dotnet publish .\src\TabletDriverCleanup\ /p:PublishSingleFile=true /p:PublishTrimmed=true /p:DebugType=embedded --configuration=Release --self-contained=true -o ./build
Copy-Item -Path ./eng/dump.bat -Destination ./build
Copy-Item -Path ./eng/dry_run.bat -Destination ./build
Copy-Item -Path ./eng/no_prompt_uninstall.bat -Destination ./build