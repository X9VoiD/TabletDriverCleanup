cargo build --release
if (Test-Path ./build) {
    Remove-Item -Path ./build -Recurse
}
New-Item -Path ./build -ItemType directory > $null
Copy-Item -Path ./target/release/tabletdrivercleanup.exe -Destination ./build
Copy-Item -Path ./eng/dump.bat -Destination ./build
Copy-Item -Path ./eng/dry_run.bat -Destination ./build
Copy-Item -Path ./eng/no_prompt_uninstall.bat -Destination ./build
Copy-Item -Path ./eng/internal_config_only.bat -Destination ./build