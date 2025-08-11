# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Build and Run
- **Build**: `dotnet build`
- **Run**: `dotnet run`
- **Clean**: `dotnet clean`
- **Publish**: `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`

### Key Project Files
- **Solution**: `GadgetTools.sln`
- **Main Project**: `GadgetTools.csproj` (WPF .NET 8 application)
- **Entry Point**: `Program.cs` contains Excel conversion utilities
- **Main Window**: `MainWindow.xaml` and `MainWindow.xaml.cs`

## Architecture Overview

### Plugin-Based Architecture
The application uses a plugin-based architecture with the following key components:

- **Plugin Interface**: `Core/Interfaces/IToolPlugin.cs` defines the contract for all plugins
- **Plugin Manager**: `Core/Services/PluginManager.cs` handles plugin loading and lifecycle
- **Plugin Directory**: `Plugins/` contains individual plugin implementations:
  - `ExcelConverter/` - Excel to various formats conversion
  - `TicketManage/` - Azure DevOps ticket management with chart functionality
  - `PullRequestManagement/` - Azure DevOps pull request management
  - Other plugin folders (Dashboard, DataAnalysis, etc.)

### Core Services
- **AzureDevOpsService**: `Services/AzureDevOpsService.cs` - Azure DevOps API integration
- **SettingsService**: `Services/SettingsService.cs` - Application settings management
- **Plugin Services**: Various services in `Core/Services/` for shared functionality

### Data Models
- **Azure DevOps Models**: `Shared/Models/AzureDevOpsModels.cs`
- **Chart Models**: `Core/Models/ChartModels.cs` for data visualization
- **Filter Models**: `Core/Models/FilterModels.cs` for data filtering functionality

### UI Framework
- **WPF with MVVM pattern**: ViewModels in `ViewModels/` and `Core/ViewModels/`
- **Custom Controls**: `Core/Controls/` contains reusable UI components
- **Styles**: `Core/Styles/` contains XAML style definitions

## Key Dependencies
- **ClosedXML**: Excel file processing
- **Microsoft.TeamFoundationServer.Client**: Azure DevOps integration
- **Newtonsoft.Json**: JSON processing
- **Markdig**: Markdown processing
- **Microsoft.Web.WebView2**: Web view control

## Data Conversion Capabilities
The main converter supports multiple output formats:
- Markdown tables
- CSV
- JSON (with proper data type handling)
- HTML with styling

## Testing and Sample Data
- **TestData/**: Contains PowerShell scripts for creating Azure DevOps test data
- **Sample Data Generator**: Creates 45 dummy work items across various categories for testing chart functionality

## Installation
- **Installer**: `installer/` directory contains WiX setup files and PowerShell installation scripts
- **Target**: Windows-only application with self-contained deployment