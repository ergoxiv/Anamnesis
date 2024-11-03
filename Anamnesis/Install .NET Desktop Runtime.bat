@echo off
title .NET Desktop Runtime Installer
echo The Microsoft .NET Desktop Runtime will be installed. Please do not close the console until it has completed.
winget install Microsoft.DotNet.DesktopRuntime.8 -e -v 8.0.10 --accept-package-agreements
