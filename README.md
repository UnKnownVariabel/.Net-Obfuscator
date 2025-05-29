## run program
**To run**\
dotnet run -- \<arguments\> \
**To build a framework dependent executable**\
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true\
**To build an executable that includes the .NET runtime**\
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true\



## Features
Supports
- Control flow obfuscation
- Renaming
- Anti-debugging
- String obfuscation
