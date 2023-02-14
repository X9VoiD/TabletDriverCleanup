dotnet publish .\src\TabletDriverCleanup\ /p:PublishSingleFile=true /p:DebugType=embedded --configuration=Debug --self-contained=false -o ./build
Copy-Item -Path ./eng/dump.bat -Destination ./build
Copy-Item -Path ./eng/dry_run.bat -Destination ./build
Copy-Item -Path ./eng/no_prompt_uninstall.bat -Destination ./build
Copy-Item -Path ./eng/internal_config_only.bat -Destination ./build