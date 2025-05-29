## Building
**To run**\
dotnet run -- \<arguments\>\
**To build a framework dependent executable**\
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true\
**To build an executable that includes the .NET runtime**\
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

## Running
**To run**\
NetShade.exe --input \<input file\> --all\
This will run the program with all obfuscation methods\
(Note: You need to have .NET 9.0 installed)

## Features
Supports
- Control flow obfuscation
- Renaming
- Anti-debugging
- String obfuscation
