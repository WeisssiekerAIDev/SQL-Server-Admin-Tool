# SQL Server Admin Tool

A modern, user-friendly management tool for Microsoft SQL Server, developed with WPF and .NET 8.0.

## Features

- SQL Server connection management
- Powerful query editor with syntax highlighting
- Execution plan visualization
- Performance monitoring
- Database management (backup/restore)
- Job management
- User management
- Template system for frequently used queries
- Export functions (CSV, Excel)

## Technical Details

- Framework: .NET 8.0
- UI Framework: WPF (Windows Presentation Foundation)
- Development Language: C#
- Code Editor: AvalonEdit
- Database Access: Microsoft.Data.SqlClient
- Logging: Serilog

## System Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Microsoft SQL Server 2016 or higher
- Minimum 4 GB RAM
- 100 MB free disk space

## Installation

1. Download the latest version from the release section
2. Extract the ZIP file to any directory
3. Start the application by double-clicking SQLServerAdmin.exe

## Development

### Prerequisites

- Visual Studio 2022 or higher
- .NET 8.0 SDK
- Git

### Build Process

```powershell
# Clone repository
git clone https://github.com/yourusername/SQLServerAdmin.git

# Change to project directory
cd SQLServerAdmin

# Restore dependencies and build
dotnet restore
dotnet build

# Start application
dotnet run --project SQLServerAdmin
```

### Main Components

- **MainWindow**: Main application window
- **QueryEditor**: Custom editor control for SQL queries
- **Services**: 
  - QueryExecutionService
  - QueryHistoryService
  - TemplateService
  - ExportService
  - IntelliSenseService

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please read our [Contribution Guidelines](CONTRIBUTING.md) for details.

## Support

If you have questions or issues:
- Create an issue on GitHub
- Submit a pull request
- Search the documentation in the docs

## Changelog

### Version 1.0.0 (February 16, 2025)
- Initial version
- Basic database functions
- Query editor with syntax highlighting
- Performance monitoring
- Backup/restore functionality
